# Free deploy: show your boss the site

Use **Render.com** (free PostgreSQL + free Web Services) so everything is free. Free tier may spin down after ~15 min idle—**open the site 1–2 minutes before the demo** so it’s warm.

---

## 1. Push your code to GitHub

Make sure your repo is on GitHub (you already use it).

---

## 2. Create a Render account and PostgreSQL

1. Go to [render.com](https://render.com) and sign up (GitHub login is fine).
2. **Dashboard** → **New +** → **PostgreSQL**.
3. **Name:** e.g. `denolite-db`. **Region:** pick one near you. **Create Database**.
4. When it’s ready, open the DB and copy:
   - **Internal Database URL** → use as `ConnectionStrings__DefaultConnection` in the **API** Web Service env vars (step 3). The API runs on Render and connects with this.
   - **External Database URL** → use when running migrations from your PC (step 4). Your machine is outside Render, so it needs this URL to reach the DB.

---

## 3. Deploy the API (Web Service)

1. **New +** → **Web Service**.
2. **Connect repository:** choose your GitHub repo. **Connect**.
3. **Name:** e.g. `denolite-api`.
4. **Region:** same as the database.
5. **Runtime:** **Docker**.
6. **Dockerfile path:** `DenoLite.Api/Dockerfile`.
7. **Instance type:** **Free**.
8. **Advanced** → **Environment Variables.** Add these (values from your `DenoLite.Api/.env.production` where noted; **never commit that file**):

   | Key | Where to get the value |
   |-----|------------------------|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__DefaultConnection` | **Render Internal Database URL** from step 2 (do *not* use the value from .env.production — that’s your local DB) |
   | `Jwt__Key` | Copy from `.env.production` |
   | `Jwt__Issuer` | After first deploy: your API URL (e.g. `https://denolite-api.onrender.com`) |
   | `Jwt__Audience` | Same as `Jwt__Issuer` |
   | `Email__Host` | Copy from `.env.production` |
   | `Email__Port` | Copy from `.env.production` (e.g. `587`) |
   | `Email__UseSsl` | Copy from `.env.production` (e.g. `true`) |
   | `Email__FromEmail` | Copy from `.env.production` |
   | `Email__FromName` | Copy from `.env.production` |
   | `Email__Username` | Copy from `.env.production` |
   | `Email__Password` | Copy from `.env.production` |
   | `Otp__Secret` | Copy from `.env.production` |
   | `Google__ClientId` | Copy from `.env.production` |
   | `Google__ClientSecret` | Copy from `.env.production` |
   | `WebApp__BaseUrl` | **Your deployed Web app URL** (e.g. `https://denolite-web.onrender.com`). Required so after Google login the API redirects to the Web app, not localhost. |

   For Google login: in Google Cloud Console add **Authorised redirect URI** `https://YOUR-API-URL/api/auth/google-callback` (e.g. `https://denolite.onrender.com/api/auth/google-callback`). Set `WebApp__BaseUrl` above to your Web URL so the API redirects back to the site after login.

   **Connection string:** Key must be exactly `ConnectionStrings__DefaultConnection` (two underscores). Value: paste the **Internal Database URL** from Render with **no quotes** around it. If you get "Format of the initialization string does not conform to specification", use the **key=value** form instead of the URI: `Host=HOST;Port=5432;Database=DATABASE;Username=USER;Password=PASSWORD;SSL Mode=Require` (get HOST, DATABASE, USER, PASSWORD from your Render DB info).

9. **Create Web Service.** Wait for the first deploy. Then copy the service URL (e.g. `https://denolite-api.onrender.com`).
10. **Environment** → set `Jwt__Issuer` and `Jwt__Audience` to that URL if you used a placeholder. **Save changes** (triggers a redeploy).

---

## 4. Run database migrations (from your PC)

Use the **External** Database URL from step 2 (so your machine can reach the DB).

```powershell
cd c:\Denis_dotnet\StudyProjects\DenoLite

$env:ConnectionStrings__DefaultConnection = "postgresql://USER:PASSWORD@HOST/DATABASE?sslmode=require"
dotnet ef database update --project DenoLite.Infrastructure --startup-project DenoLite.Api
```

Replace the connection string with the **External Database URL** from Render (it already includes user, password, host, DB; add `?sslmode=require` if missing).

---

## 5. Deploy the Web app (second Web Service)

1. **New +** → **Web Service**.
2. Same repo. **Name:** e.g. `denolite-web`.
3. **Runtime:** **Docker**. **Dockerfile path:** `DenoLite.Web/Dockerfile`.
4. **Instance type:** **Free**.
5. **Environment Variables:**

   | Key | Value |
   |-----|--------|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `Api__BaseUrl` | *(API URL from step 3, e.g. `https://denolite-api.onrender.com`)* |

6. **Create Web Service.** Copy the Web URL (e.g. `https://denolite-web.onrender.com`).

---

## 6. CORS on the API

1. Open the **API** Web Service on Render → **Environment**.
2. Add:
   - **Key:** `Cors__AllowedOrigins`
   - **Value:** your Web URL, e.g. `https://denolite-web.onrender.com` (multiple URLs: separate with `;`).

The API reads this from config so the Web app can call it.

---

## 7. Demo checklist

- [ ] Migrations run (step 4).
- [ ] API env vars set, including JWT and DB URL (step 3).
- [ ] Web env var `Api__BaseUrl` = API URL (step 5).
- [ ] CORS allows the Web URL (step 6).
- [ ] **1–2 minutes before the demo:** open the **Web** URL in the browser so the free instance wakes up (no “loading” delay in front of your boss).

---

## If deploy fails with "Exited with status 139"

Exit 139 often means the process was killed (e.g. out of memory) or a runtime crash.

1. **Env var names** – In Render use **two underscores** for nested config: `Jwt__Issuer`, `Jwt__Audience`, `ConnectionStrings__DefaultConnection` (not `Jwt_Issuer`). Single underscore does not map to `Jwt:Issuer`.
2. **Check full logs** – In Render open **Logs** for the service and scroll up. Look for "Out of memory", "Killed", or an exception message before the exit.
3. **GC limit** – The API Dockerfile sets `DOTNET_GCHeapHardLimit` to reduce OOM risk on the free tier. Commit and redeploy.
4. **Try .NET 9** – If it still fails, switch the API (and Web) to .NET 9 for deploy: in both Dockerfiles use `sdk:9.0` and `aspnet:9.0`, and set `TargetFramework` to `net9.0` in `DenoLite.Api.csproj` and `DenoLite.Web.csproj` (and downgrade package versions to 9.x). Then redeploy.

---

## If Render Docker build fails (.NET 10)

If the build fails with an error about the .NET 10 image, change both Dockerfiles to use **9.0** and temporarily set **TargetFramework** to `net9.0` in:

- `DenoLite.Api/DenoLite.Api.csproj`
- `DenoLite.Web/DenoLite.Web.csproj`

Then redeploy.

---

## Alternative: Azure free + free Postgres

- Create **2 × App Service** (Free F1): one for API, one for Web.
- Use a **free PostgreSQL** at [Neon](https://neon.tech) or [Supabase](https://supabase.com).
- Set `ConnectionStrings__DefaultConnection` (Neon/Supabase URL) and JWT/Email in the API App Service; set `Api__BaseUrl` in the Web App Service.
- Publish from Visual Studio (Publish → Azure) or `dotnet publish` + zip deploy.
- Run migrations from your PC using the Neon/Supabase connection string.

No Docker needed; good if you prefer Azure and only need a quick demo.
