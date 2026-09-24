# Deployment

How Finance Tracker runs in production, and the one-time steps to stand it up. Sized for a
closed test by a couple of people first — sign-up stays on the UI's allowlist — on free tiers,
in Canada Central. Opening it to everyone later is a configuration change, not a move.

| Piece | Runs on | Cost at this size |
|---|---|---|
| UI + BFF (`finance-tracker-ui`) | Vercel Hobby, functions pinned to Montréal (`yul1`) | Free |
| API | Azure Container Apps, scales to zero | Inside the monthly free grant |
| Worker | Azure Container Apps **Job**, on a daily cron | Inside the same grant |
| Database | Azure SQL Database, serverless, free offer | Free up to 100,000 vCore-seconds a month |
| Images | GitHub Container Registry | Free |

The database holds the real financial records once step 2 is done. Everything in
CLAUDE.md's data-safety section applies to it exactly as it does to the local one.

## One-time setup

Run the `az` blocks in **Azure Cloud Shell (Bash)** — it is already signed in and has the CLI.
The data copy in step 2 runs on your own machine, because that is where the local database is.

Set these once per Cloud Shell session:

```bash
RG=rg-finance-tracker
LOC=canadacentral
SQL=sql-finance-tracker-<something unique>   # becomes <name>.database.windows.net
GH_USER=<your GitHub username, lowercase>   # image paths on ghcr.io are lowercase
```

### 0. Before you start

- **Merge this deployment change to `main`.** CI runs, then the Deploy workflow builds and
  pushes `ghcr.io/<owner>/finance-tracker-api:main` and `…/finance-tracker-worker:main`.
  Its `deploy` job is skipped until step 5 — that is expected, not a failure.
- **A GitHub classic personal access token with only `read:packages`.** Container Apps uses
  it to pull the private images. (Fine-grained tokens do not cover packages.)
- **Generate the secrets** (`openssl rand -base64 48` in Cloud Shell, once each):
  the JWT signing key, the BFF shared secret (the same value goes to Vercel as
  `API_BFF_SECRET`), and two SQL passwords — one for the server admin, one for the app.
  Keep them in a password manager; nothing here writes them to a file.
- **A budget alert.** Portal → Cost Management → Budgets → a $5 monthly budget emailing you.
  Everything below should cost nothing; this is how you find out if it doesn't.

### 1. Resource group and database

Find your local database's collation first, and create the Azure one with the same. Category
names are unique per `(UserId, CategoryType, Name)` *under the database's collation* (see
CLAUDE.md, Households) — a different collation would change what counts as a duplicate.

```sql
-- Against your LOCAL database
SELECT DATABASEPROPERTYEX(DB_NAME(), 'Collation');
```

```bash
az group create -n $RG -l $LOC

az sql server create -g $RG -n $SQL -l $LOC \
  --admin-user ftadmin --admin-password '<admin password>'

# --use-free-limit is the free offer. BillOverUsage keeps the app up if a month ever runs
# over, rather than pausing the database until the 1st; set it now, at creation.
az sql db create -g $RG -s $SQL -n financetracker \
  --edition GeneralPurpose --compute-model Serverless --family Gen5 --capacity 2 \
  --use-free-limit --free-limit-exhaustion-behavior BillOverUsage \
  --collation '<collation from the query above>'

# Container Apps has no fixed outbound address, so it connects as "an Azure service".
# That admits connection attempts from any Azure tenant; credentials still gate them.
az sql server firewall-rule create -g $RG -s $SQL -n AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```

Then in the portal, on the SQL server's **Networking** page, choose **Add your client IPv4
address** so your own machine can reach it for step 2 and for future migrations.

### 2. Copy your records (on your machine)

1. **Stop everything that writes locally**: the API, and the Windows Task Scheduler task
   that runs the worker. An export taken mid-write is an inconsistent copy.
2. **Confirm the local database is fully migrated** — the export carries its schema and
   `__EFMigrationsHistory` with it, so this is also what the cloud database will be at:
   ```bash
   dotnet ef migrations list --project FinanceTracker.Infrastructure --startup-project FinanceTracker
   ```
   Nothing should be marked `(Pending)`.
3. **Export** with SqlPackage (`dotnet tool install -g microsoft.sqlpackage`). Write the file
   **outside the repository** — it is every record you have. (`*.bacpac` is git- and
   docker-ignored as a backstop, not as the plan.)
   ```
   sqlpackage /Action:Export /SourceConnectionString:"<local connection string>" /TargetFile:"C:\temp\financetracker.bacpac"
   ```
   The local connection string is the one in `dotnet user-secrets list --project FinanceTracker/FinanceTracker.API.csproj`.
4. **Import** into the database created in step 1. Importing into that existing, empty
   database is what keeps the free offer — letting the import create its own would make a
   paid one.
   ```
   sqlpackage /Action:Import /SourceFile:"C:\temp\financetracker.bacpac" /TargetConnectionString:"Server=tcp:<SQL>.database.windows.net,1433;Initial Catalog=financetracker;User ID=ftadmin;Password=<admin password>;Encrypt=True;Connect Timeout=60;"
   ```
5. **Verify** by running this read-only query against both databases and comparing:
   ```sql
   SELECT t.name, SUM(p.rows) AS [rows]
   FROM sys.tables t
   JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
   GROUP BY t.name ORDER BY t.name;
   ```
6. **Delete the `.bacpac`** once the counts match.

### 3. A login for the app

The server admin can alter and drop anything; the API and worker only ever read and write
rows (migrations are applied by hand, as the admin). Give them a login that can do only that.
Run in the portal's **Query editor** on `financetracker`, signed in as `ftadmin`:

```sql
CREATE USER ft_app WITH PASSWORD = '<app password>';
ALTER ROLE db_datareader ADD MEMBER ft_app;
ALTER ROLE db_datawriter ADD MEMBER ft_app;
```

The **app connection string** used below is then:

```
Server=tcp:<SQL>.database.windows.net,1433;Initial Catalog=financetracker;User ID=ft_app;Password=<app password>;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60;
```

`Connect Timeout=60` and the retrying execution strategy in `Program.cs` are both there for
the same reason: the first connection after the database has auto-paused waits while it
resumes.

### 4. API and worker

```bash
az extension add --name containerapp --upgrade
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights

az containerapp env create -g $RG -n cae-finance-tracker -l $LOC

az containerapp create -g $RG -n ca-finance-tracker-api --environment cae-finance-tracker \
  --image ghcr.io/$GH_USER/finance-tracker-api:main \
  --registry-server ghcr.io --registry-username $GH_USER --registry-password '<read:packages token>' \
  --ingress external --target-port 8080 \
  --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi \
  --secrets "db-connection=<app connection string>" \
            "jwt-signing-key=<JWT signing key>" \
            "bff-shared-secret=<BFF shared secret>" \
  --env-vars ConnectionStrings__FinanceTrackerDB=secretref:db-connection \
             Jwt__SigningKey=secretref:jwt-signing-key \
             Auth__BffSharedSecret=secretref:bff-shared-secret \
             Auth__AppBaseUrl=https://placeholder.invalid \
             Email__Provider=Logging \
             ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

az containerapp job create -g $RG -n caj-finance-tracker-worker --environment cae-finance-tracker \
  --trigger-type Schedule --cron-expression "0 12 * * *" \
  --replica-timeout 1800 --replica-retry-limit 1 --parallelism 1 --replica-completion-count 1 \
  --image ghcr.io/$GH_USER/finance-tracker-worker:main \
  --registry-server ghcr.io --registry-username $GH_USER --registry-password '<read:packages token>' \
  --cpu 0.25 --memory 0.5Gi \
  --secrets "db-connection=<app connection string>" \
  --env-vars ConnectionStrings__FinanceTrackerDB=secretref:db-connection
```

Check the API: `curl https://$(az containerapp show -g $RG -n ca-finance-tracker-api --query properties.configuration.ingress.fqdn -o tsv)/healthz`
should answer `Healthy`. Run the worker once by hand with
`az containerapp job start -g $RG -n caj-finance-tracker-worker` and look at
`az containerapp job execution list -g $RG -n caj-finance-tracker-worker -o table`.

Why these settings:

- **`ASPNETCORE_FORWARDEDHEADERS_ENABLED`** — TLS ends at the Container Apps ingress, which
  forwards plain HTTP. This built-in switch makes the API honour `X-Forwarded-Proto` and
  `X-Forwarded-For`, so it knows the request was HTTPS. A startup warning that it
  "failed to determine the https port for redirect" is expected and harmless.
- **`--max-replicas 1`** — the rate limiter counts in memory, so a second replica would double
  every limit. One replica is also a ceiling on the bill.
- **`--min-replicas 0`** — scale to zero. The first request after a quiet spell takes a few
  seconds to start the API, and longer if the database has paused too.
- **Email is `Logging`** while it is just the two of you, so nothing is mailed. Signing in
  with Google marks the address as confirmed (which households need), and a household
  invitation appears on the invitee's households page whether or not it is emailed.

### 5. Deploy on merge

The Deploy workflow signs in to Azure with a federated credential — no password stored in
GitHub. Create one that trusts only this repository's `main`:

```bash
APP_ID=$(az ad app create --display-name gh-finance-tracker-deploy --query appId -o tsv)
az ad sp create --id $APP_ID
az role assignment create --assignee $APP_ID --role Contributor \
  --scope $(az group show -n $RG --query id -o tsv)
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<owner>/finance-tracker-api:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'

echo "AZURE_CLIENT_ID=$APP_ID"
echo "AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv)"
```

If the first deploy fails at Azure login with *no matching federated identity record*, the
error quotes the subject GitHub actually presented — repositories created, renamed or
transferred after 15 July 2026 use an immutable form with numeric ids in it. Recreate the
credential with that exact subject.

Add those three as **repository variables** (Settings → Secrets and variables → Actions →
Variables). They are identifiers, not secrets. From then on every merge to `main` that passes
CI is deployed; run the Deploy workflow by hand to redeploy.

### 6. Front end (Vercel)

1. Import `finance-tracker-ui` into Vercel. Its `vercel.json` pins functions to Montréal, so
   the BFF — which every request's financial data passes through — runs in Canada, next to
   the API, rather than Vercel's default of Washington, D.C.
2. Add the environment variables, scoped to **Production only**. Preview deployments of pull
   requests must not hold credentials that reach real records.

   | Variable | Value |
   |---|---|
   | `API_URL` | `https://<API fqdn from step 4>/api` |
   | `NEXT_PUBLIC_APP_URL` | `https://<your project>.vercel.app` |
   | `AUTH_SECRET` | `npx auth secret` |
   | `API_BFF_SECRET` | the BFF shared secret from step 0 |
   | `AUTH_SIGNUP_MODE` | `allowlist` |
   | `AUTH_ALLOWED_EMAILS` | your two addresses, comma-separated |
   | `AUTH_GOOGLE_ID`, `AUTH_GOOGLE_SECRET` | the Google OAuth client |

   `NEXT_PUBLIC_APP_URL` is baked in at build time — redeploy after setting it.
3. In Google Cloud Console, add `https://<your project>.vercel.app/api/auth/callback/google` as
   an authorized redirect URI, and add both of you as test users while the consent screen is
   in Testing. Google's account id is the same for every OAuth client, so the identities you
   copied in step 2 still match.
4. Point the API's links at the real address:
   `az containerapp update -g $RG -n ca-finance-tracker-api --set-env-vars Auth__AppBaseUrl=https://<your project>.vercel.app`

### 7. Cut over

- **Leave the local worker's scheduled task disabled** (step 2 stopped it). The Container
  Apps Job does its work now.
- **The Azure database is now the one that matters.** The local copy is a snapshot as of the
  export; it still holds real records, so keep treating it that way, but new entries go to
  the cloud from here on.

## Shipping changes

- **Merge to `main`.** CI tests it; the Deploy workflow then builds both images, points the
  API and the worker at them, and checks `/healthz`.
- **A change with a migration is migrated by hand before it is merged**, from your machine,
  as the admin:
  ```bash
  dotnet ef database update --project FinanceTracker.Infrastructure --startup-project FinanceTracker \
    --connection "<admin connection string>"
  ```
  The running version keeps serving against the new schema until the merge deploys, so
  prefer migrations that only add. Never from a cloud or remote session.

## Keeping it free

- **The database is billed for every second it is awake**, and each wake-up keeps it awake
  until its auto-pause delay passes. The worker's daily run is one wake-up a day before
  anyone signs in. Set the auto-pause delay to the portal's minimum, and consider moving
  the cron to a time you would be using the app anyway.
- **Any cron time from 08:00 to 23:59 UTC is the same calendar date everywhere in Canada.**
  Outside that window a run happens on the previous local evening, so a recurring transaction
  dated the 1st shows up the night before. (Scheduling across time zones properly is part of
  the date/time work, not this.)
- **`/healthz` never touches the database** — `HealthEndpointIntegrationTests` fails if it
  starts to. A health check that queried it would keep it from ever pausing.

## Before opening sign-up

This setup is right for a closed test and deliberately incomplete for strangers:

- **Email** — a domain and Resend (`Email__Provider=Resend`); password reset and magic links
  need it.
- **Rate limits are per BFF, not per person.** The API partitions them by client address, and
  every request comes from Vercel's servers, so all users share one bucket.
- **The category-delete cascade** in CLAUDE.md's Households section: one member can erase
  another's history with no undo.
- **Dates** — the dashboard turns its local date ranges into UTC instants and compares them
  with calendar dates, so for anyone outside UTC a range is off by a day.
- **SQL by managed identity** instead of a password, and a staging database to rehearse
  migrations on.
- **Vercel Hobby is non-commercial** — Pro, or the UI on Container Apps too, if that changes.
