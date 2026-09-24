# Deployment

How Finance Tracker runs in production, and the one-time steps to stand it up. Sized for a
closed test by a couple of people first, on free tiers, in Canada Central. Sign-up stays
closed: the API answers auth calls only from the UI's server (the `X-Bff-Secret` header), so
the UI's `AUTH_SIGNUP_MODE=allowlist` decides who gets in. Opening it to everyone later is a
configuration change, not a move.

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

Run the `az` blocks in **Azure Cloud Shell (Bash)**, choosing **No storage account required**
when it asks: it is already signed in, has the CLI, and keeps no files or history once the
session ends. The data copy in step 2 runs on your own machine, because that is where the
local database is.

Set these at the start of each Cloud Shell session. `read -rs` takes each secret without
echoing it or writing it to shell history, and the commands below refer to the variables —
so no secret is typed into a command or saved in history (they still reach `az` as arguments,
inside a session that is discarded when it ends).

```bash
RG=rg-finance-tracker
LOC=canadacentral
SQL=sql-finance-tracker-<something unique>   # becomes <name>.database.windows.net
GH_USER=<your GitHub username, lowercase>   # image paths on ghcr.io are lowercase

read -rsp "SQL admin password: " ADMIN_PW; echo
read -rsp "SQL app password: " APP_PW; echo
read -rsp "JWT signing key: " JWT_KEY; echo
read -rsp "BFF shared secret: " BFF_SECRET; echo
read -rsp "GitHub read:packages token: " GHCR_TOKEN; echo

APP_CONN="Server=tcp:$SQL.database.windows.net,1433;Initial Catalog=financetracker;User ID=ft_app;Password=$APP_PW;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60;"
```

### 0. Before you start

- **Merge the UI change first, then this one.** The API now answers auth calls only when
  they carry the shared secret, and only the updated UI sends it on every call — an old UI
  would still sign in with Google but fail every token refresh, signing everyone out within
  15 minutes, with password and magic-link sign-in dead. The updated UI works against the old
  API (which ignores the extra header), so UI first is safe in either environment, including
  local development.
- **Merge this deployment change to `main`.** CI runs, then the Deploy workflow builds and
  pushes `ghcr.io/<owner>/finance-tracker-api:main` and `…/finance-tracker-worker:main`.
  Its `deploy` job is skipped until step 5 — that is expected, not a failure.
- **A GitHub classic personal access token with only `read:packages`.** Container Apps uses
  it to pull the private images. (Fine-grained tokens do not cover packages.) **Note its
  expiry date and set a reminder**: when it expires, the next scale-up from zero or deploy
  cannot pull the image and the API goes down. Renew with
  `az containerapp registry set -g $RG -n ca-finance-tracker-api --server ghcr.io --username $GH_USER --password "$GHCR_TOKEN"`
  and the same for the job (`az containerapp job registry set … -n caj-finance-tracker-worker`).
- **Generate the secrets** (`openssl rand -base64 48` in Cloud Shell, once each): the JWT
  signing key, the BFF shared secret (the same value goes to Vercel as `API_BFF_SECRET`), and
  two SQL passwords — one for the server admin, one for the app. Keep them in a password
  manager.
- **Two budget alerts.** Portal → Cost Management → Budgets → a monthly budget with alerts at
  $1 and $5. Everything below should cost nothing, and the database cannot bill (see step 1);
  this is how you find out if something else does. A budget only emails — it never stops
  anything — and the email can lag by up to a day.

### 1. Resource group and database

Find your local database's collation first, and create the Azure one with the same. Category
names are unique per `(UserId, CategoryType, Name)` *under the database's collation* (see
CLAUDE.md, Households), and EF's migrations name no collation, so every column takes the
database default — a different one would change what counts as a duplicate.

```sql
-- Against your LOCAL database
SELECT DATABASEPROPERTYEX(DB_NAME(), 'Collation');
```

```bash
az group create -n $RG -l $LOC

az sql server create -g $RG -n $SQL -l $LOC \
  --admin-user ftadmin --admin-password "$ADMIN_PW"

# --use-free-limit is the free offer. AutoPause is a hard $0 ceiling: if a month ever uses up
# the free allowance, the database pauses until the 1st rather than billing. It can later be
# switched to BillOverUsage (the app stays up and the excess is billed) — but BillOverUsage
# can never be switched back, so it waits until sign-up opens.
#
# --auto-pause-delay 15 is the minimum: every wake-up is billed until the delay runs out.
az sql db create -g $RG -s $SQL -n financetracker \
  --edition GeneralPurpose --compute-model Serverless --family Gen5 --capacity 2 \
  --use-free-limit --free-limit-exhaustion-behavior AutoPause \
  --auto-pause-delay 15 \
  --collation '<collation from the query above>'

# Container Apps has no fixed outbound address, so it connects as "an Azure service".
# That admits connection attempts from any Azure tenant; credentials still gate them.
az sql server firewall-rule create -g $RG -s $SQL -n AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```

Then in the portal, on the SQL server's **Networking** page, choose **Add your client IPv4
address** so your own machine can reach it for step 2 and for future migrations. The
database's overview page should show this month's remaining free amount — that is how you
know the free offer, not the free account's 12-month SQL deal, is the one applied. Confirm the
pause delay took, too: `az sql db show -g $RG -s $SQL -n financetracker --query autoPauseDelay`
should print `15`; if not, set it on the database's **Compute + storage** page.

### 2. Copy your records (on your machine)

The commands here carry passwords, and shells can keep them in a history file. Recent
PowerShell 7 versions skip saving lines that look like they hold a password; others may not.
Check yours, and remove those lines afterwards.

1. **Stop everything that writes locally, for good.** *Disable* (not just stop) the Windows
   Task Scheduler task that runs the worker — a stopped task still fires on its next trigger —
   and stop the local API. From here on, enter nothing locally: it would never reach Azure.
2. **Confirm the local database is fully migrated** — the export carries its schema and
   `__EFMigrationsHistory` with it, so this is also what the cloud database will be at:
   ```bash
   dotnet ef migrations list --project FinanceTracker.Infrastructure --startup-project FinanceTracker
   ```
   Nothing should be marked `(Pending)`.
3. **Freeze the database, then back it up.** An export is not a transactional snapshot: it
   reads table by table, and a write landing mid-export (the worker advancing a template, say)
   can produce a copy that row counts will not catch. Read-only makes that impossible, and
   freezing first makes the backup and the export the same moment. The backup is the
   pre-cloud copy of everything — keep it (see *Backups* below for what comes after).
   ```sql
   -- Against your LOCAL database. The .bak lands in SQL Server's default backup folder;
   -- it is every record you have, so keep it somewhere private.
   ALTER DATABASE [<local database name>] SET READ_ONLY WITH ROLLBACK IMMEDIATE;
   BACKUP DATABASE [<local database name>] TO DISK = N'financetracker-pre-cloud.bak' WITH COPY_ONLY, CHECKSUM;
   RESTORE VERIFYONLY FROM DISK = N'financetracker-pre-cloud.bak' WITH CHECKSUM;
   ```
4. **Export** with SqlPackage (`dotnet tool install -g microsoft.sqlpackage`). Write the file
   **outside the repository** — it is every record you have. (`*.bacpac` is git- and
   docker-ignored as a backstop, not as the plan.)
   ```
   sqlpackage /Action:Export /SourceConnectionString:"<local connection string>" /TargetFile:"C:\temp\financetracker.bacpac"
   ```
   The local connection string is the one in `dotnet user-secrets list --project FinanceTracker/FinanceTracker.API.csproj`.
5. **Import** into the database created in step 1. Importing into that existing, empty
   database is what keeps the free offer — letting the import create its own would make a
   paid one.
   ```
   sqlpackage /Action:Import /SourceFile:"C:\temp\financetracker.bacpac" /TargetConnectionString:"Server=tcp:<SQL>.database.windows.net,1433;Initial Catalog=financetracker;User ID=ftadmin;Password=<admin password>;Encrypt=True;Connect Timeout=60;"
   ```
6. **Verify.** Run all of these against **both** databases; every result should match.
   ```sql
   -- Exact row count of every table (sys.partitions only approximates). Needs SQL Server 2017+.
   DECLARE @sql nvarchar(max) = (
       SELECT STRING_AGG(CAST(N'SELECT N''' + name + N''' AS [table], COUNT_BIG(*) AS [rows] FROM '
               + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) AS nvarchar(max)), N' UNION ALL ')
       FROM sys.tables) + N' ORDER BY [table];';
   EXEC sp_executesql @sql;

   -- The money itself, per person.
   SELECT UserId, COUNT_BIG(*) AS [transactions], SUM(Amount) AS [total]
   FROM Transactions GROUP BY UserId ORDER BY UserId;

   -- Where every recurring template is up to; a half-copied worker run shows here.
   SELECT COUNT_BIG(*) AS [templates],
          CHECKSUM_AGG(BINARY_CHECKSUM(Id, NextOccurrenceDate, Status)) AS [state]
   FROM RecurringTransactions;

   -- Schema version and the collation category uniqueness depends on.
   SELECT (SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC) AS [latest_migration],
          DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS [database_collation],
          (SELECT collation_name FROM sys.columns
           WHERE object_id = OBJECT_ID('Categories') AND name = 'Name') AS [category_name_collation];
   ```
7. **Check every account is verified.** Once live, the first Google sign-in to an account
   nobody has verified removes its password and ends its sessions — the defence against a
   stranger pre-registering someone's address. Email is not being sent yet, so a password lost
   that way cannot be reset. Run against Azure:
   ```sql
   SELECT u.Email, u.EmailVerifiedAt, i.Provider
   FROM Users u LEFT JOIN UserIdentities i ON i.UserId = u.Id
   ORDER BY u.Email;
   ```
   Any row with a null `EmailVerifiedAt` is an account whose password will not survive its
   owner's first Google sign-in. For the two of you, signing in with Google is enough; just
   know it will happen.
8. **Delete the `.bacpac`** once everything matches — with Shift+Delete, or empty the Recycle
   Bin afterwards. Keep the `.bak` from step 3.

### 3. A login for the app

The server admin can alter and drop anything; the API and worker only ever read and write
rows (migrations are applied by hand, as the admin). Give them a login that can do only that.
Run in the portal's **Query editor** on `financetracker`, signed in as `ftadmin`, with the app
password in place of the placeholder:

```sql
CREATE USER ft_app WITH PASSWORD = '<app password>';
ALTER ROLE db_datareader ADD MEMBER ft_app;
ALTER ROLE db_datawriter ADD MEMBER ft_app;
```

`$APP_CONN` (set at the top) is this login's connection string. Its `Connect Timeout=60` and
the retrying execution strategy in `Program.cs` are both there for the same reason: the first
connection after the database has auto-paused waits while it resumes.

### 4. API and worker

```bash
az extension add --name containerapp --upgrade
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights

az containerapp env create -g $RG -n cae-finance-tracker -l $LOC

az containerapp create -g $RG -n ca-finance-tracker-api --environment cae-finance-tracker \
  --image ghcr.io/$GH_USER/finance-tracker-api:main \
  --registry-server ghcr.io --registry-username $GH_USER --registry-password "$GHCR_TOKEN" \
  --ingress external --target-port 8080 \
  --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi \
  --secrets "db-connection=$APP_CONN" \
            "jwt-signing-key=$JWT_KEY" \
            "bff-shared-secret=$BFF_SECRET" \
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
  --registry-server ghcr.io --registry-username $GH_USER --registry-password "$GHCR_TOKEN" \
  --cpu 0.25 --memory 0.5Gi \
  --secrets "db-connection=$APP_CONN" \
  --env-vars ConnectionStrings__FinanceTrackerDB=secretref:db-connection
```

Check the API: `curl -i https://$(az containerapp show -g $RG -n ca-finance-tracker-api --query properties.configuration.ingress.fqdn -o tsv)/healthz`
should answer `Healthy`, with the commit it was built from in `X-Source-Sha`. Run the worker
once by hand — at any time except around its scheduled 12:00 UTC run — with
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
- **Email is `Logging`** while it is just the two of you, so nothing is mailed — and the log
  records only who a message was for and its subject, never the body, because the bodies are
  live sign-in and reset links. Signing in with Google marks the address as confirmed (which
  households need), and a household invitation appears on the invitee's households page
  whether or not it is emailed.

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

The workflow deploys a commit only while it is still the tip of `main`, so a late or re-run CI
of an older commit cannot roll production back. Its smoke test waits until `/healthz` reports
the new commit, so it fails if the new revision never starts rather than passing on the old one.

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

- **Leave the local worker's scheduled task disabled.** The Container Apps Job does its work
  now.
- **The Azure database is now the one that matters.** The local copy is a snapshot as of the
  export; it still holds real records, so keep treating it that way. Leaving it `READ_ONLY`
  (step 3) is the simplest way to make sure nothing is entered there by mistake;
  `ALTER DATABASE [<local database name>] SET READ_WRITE;` undoes it when you need to develop
  against it.

## Shipping changes

- **Merge to `main`.** CI tests it; the Deploy workflow then builds both images, points the
  API and the worker at them, and waits for `/healthz` to report the new commit.
- **A change with a migration is migrated by hand before it is merged**, from your machine,
  as the admin. The running version serves against the new schema until the merge deploys,
  so:
  - **Apply only from the final commit** that will be merged. A migration applied from a
    branch that is later revised or abandoned leaves production with a schema `main` does not
    know about.
  - **Read the SQL first**:
    `dotnet ef migrations script --idempotent --project FinanceTracker.Infrastructure --startup-project FinanceTracker`.
  - **New columns must be nullable or have a default.** A `NOT NULL` column without one breaks
    every insert the running API and worker make until the deploy lands.
  - **Back up first** when a migration transforms existing values rather than only adding —
    the date/time rework will be one of those.
  ```bash
  dotnet ef database update --project FinanceTracker.Infrastructure --startup-project FinanceTracker \
    --connection "<admin connection string>"
  ```
  Never from a cloud or remote session.

## Backups

Azure's automatic backups on the free offer reach back **7 days** and nothing further — no
long-term retention and no database copy. The pre-cloud `.bak` covers nothing entered after the
cutover. Anything noticed later than a week — a category deleted and its transactions cascaded
with it, say — is gone unless you have your own copy.

So take one **monthly**, from your machine, and keep it somewhere private and off the repo:

```
sqlpackage /Action:Export /SourceConnectionString:"Server=tcp:<SQL>.database.windows.net,1433;Initial Catalog=financetracker;User ID=ftadmin;Password=<admin password>;Encrypt=True;Connect Timeout=60;" /TargetFile:"<private folder>\financetracker-<yyyy-mm>.bacpac"
```

This wakes the database once, like any other visit. A restore is an import into a new, empty
database (step 2's import), never over the live one.

## Keeping it free

- **The database is billed for every second it is awake**, and each wake-up keeps it awake
  until its auto-pause delay (15 minutes) passes. The free allowance is roughly 55 hours awake
  a month; the worker's daily run uses about 8 of them before anyone signs in. Consider moving
  the cron to a time you would be using the app anyway.
- **With AutoPause, running out means the app is down until the 1st**, not a bill. The
  database's overview page shows how much of the month's allowance is left.
- **Any cron time from 08:00 to 23:59 UTC is the same calendar date everywhere in Canada.**
  Outside that window a run happens on the previous local evening, so a recurring transaction
  dated the 1st shows up the night before. (Scheduling across time zones properly is part of
  the date/time work, not this.)
- **`/healthz` never touches the database** for the anonymous callers that poll it —
  `HealthEndpointIntegrationTests` fails if it starts to. A health check that queried it would
  keep it from ever pausing.

## Before opening sign-up

This setup is right for a closed test and deliberately incomplete for strangers:

- **Billing** — switch the database to `BillOverUsage` so running out of free allowance no
  longer takes the app down. That switch is one-way.
- **Email** — a domain and Resend (`Email__Provider=Resend`); password reset and magic links
  need it.
- **Rate limits are per BFF, not per person.** The API partitions them by client address, and
  every request comes from Vercel's servers, so all users share one bucket — fed by the
  anonymous account routes (forgot password, magic link) anyone can call. Refresh and SSO
  exchange are exempt, so a flood cannot sign existing sessions out, but it can still block
  password and magic-link sign-in for everyone. Before strangers arrive: a Vercel firewall
  rate rule on `/api/account/*` and the credentials callback, and a limit keyed on the real
  client address.
- **The category-delete cascade** in CLAUDE.md's Households section: one member can erase
  another's history with no undo.
- **Dates** — the dashboard turns its local date ranges into UTC instants and compares them
  with calendar dates, so for anyone outside UTC a range is off by a day.
- **SQL by managed identity** instead of a password, a narrower role than Contributor for the
  deploy identity (Contributor can read the Container App's secrets), and a staging database
  to rehearse migrations on.
- **Vercel Hobby is non-commercial** — Pro, or the UI on Container Apps too, if that changes.
