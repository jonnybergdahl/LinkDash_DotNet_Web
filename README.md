# LinkDash_DotNet_Web
A simple link grouping web for organizing your link.

Built with ASP.NET Core MVC on .NET 10, with a Material Design 3 inspired look (light and dark themes).

## Projects

| Project | Description |
| --- | --- |
| `src/Bergdahl.LinkDash.Web` | MVC web app: the dashboard (`/`) and the settings pages for groups (`/Settings`) and links (`/Settings/Links`, one tab per group). |
| `src/Bergdahl.LinkDash.Core` | Models, JSON file storage and the link service. |
| `src/Bergdahl.LinkDash.Tests` | NUnit unit tests and in-memory integration tests. |

## Running

```shell
dotnet run --project src/Bergdahl.LinkDash.Web
```

```shell
dotnet test
```

## Configuration

Groups and links are stored in a single JSON file. Set its location in `appsettings.json`:

```json
"LinkDash": {
  "DataFilePath": "App_Data/links.json"
}
```

Relative paths are resolved against the web app's content root. The file is created on the first change.
You can also set the path with the environment variable `LinkDash__DataFilePath`.

Groups and links can both have an icon. Group icons are shown next to the group name; groups without one show just the name, and links without one get a default link icon.

Icons are optional [Material Design Icons](https://pictogrammers.com/library/mdi/) names, written the same way as in Home Assistant: `mdi:home`, `mdi:email` or `mdi:github`. The `mdi:` prefix is optional when typing.
The icon field has a searchable dropdown, like the icon picker in Home Assistant, that matches icon names and aliases.

The dropdown's icon list lives in `src/Bergdahl.LinkDash.Web/wwwroot/lib/mdi/icons.json`. When you upgrade the MDI font version in `_Layout.cshtml`, regenerate the list with the same version:

```shell
node tools/generate-mdi-icons.mjs 7.4.47
```

## Signing in

The dashboard is public, but the settings pages require signing in as the administrator. There is a single administrator account.

The first time anyone opens the settings pages, LinkDash asks them to create that account: a user name (default `admin`, not case sensitive) and a password of at least 6 characters.
The account is saved in `admin.json` next to the data file. The password is never stored, only a salted PBKDF2 hash (the same format ASP.NET Core Identity uses).

> Whoever opens the settings pages first becomes the administrator, so create the account right after installing, before the app is reachable by others.

To reset a forgotten password, delete `admin.json`; the next visit to the settings pages asks for a new account.

### Setting the account in configuration (optional)

Instead of creating the account in the browser, you can configure it in the `LinkDash:Admin` section, for example for automated deployments.
A configured password hash takes precedence over `admin.json`:

```json
"LinkDash": {
  "Admin": {
    "Username": "admin",
    "PasswordHash": "<hash from the hash-password command>"
  }
}
```

Create the hash with the app's `hash-password` command, which asks for the password twice and prints the hash:

```shell
dotnet run --project src/Bergdahl.LinkDash.Web -- hash-password
```

On a server, run the published app instead: `dotnet Bergdahl.LinkDash.Web.dll hash-password`.
You can also use the environment variables `LinkDash__Admin__Username` and `LinkDash__Admin__PasswordHash`, or user secrets during development:

```shell
dotnet user-secrets --project src/Bergdahl.LinkDash.Web set "LinkDash:Admin:PasswordHash" "<hash>"
```

### Sessions

- Changing the user name or password, or deleting `admin.json`, signs out everyone who is signed in. Configuration changes take effect without a restart.
- Sessions last 14 days and are renewed while in use. Without *Keep me signed in*, the session ends when the browser is closed.
- An IP address gets at most 10 sign-in attempts per minute.
- The keys that protect the sign-in cookie are stored in a `keys` folder next to the data file (encrypted with DPAPI on Windows), so restarts don't sign anyone out.

Serve the app over HTTPS when it is reachable from other machines, so the password and the sign-in cookie aren't sent in clear text.

## Deploying to IIS

The steps below use the site name `LinkDash` and the folder `C:\inetpub\LinkDash`; change them as you like.
Run the PowerShell commands on the server in an elevated (administrator) prompt.

### 1. Prepare the server

1. Enable IIS (*Turn Windows features on or off* on Windows, or *Add Roles and Features* → *Web Server (IIS)* on Windows Server).
2. Install the **ASP.NET Core 10.0 Hosting Bundle** from the [.NET 10 download page](https://dotnet.microsoft.com/download/dotnet/10.0) (under *ASP.NET Core Runtime* → *Windows* → *Hosting Bundle*). Install IIS first; if you installed the bundle before IIS, run the bundle installer again (choose *Repair*).
3. Restart IIS so it picks up the new module:

   ```powershell
   net stop was /y
   net start w3svc
   ```

### 2. Publish the app

On your development machine:

```shell
dotnet publish src/Bergdahl.LinkDash.Web -c Release -o publish
```

Copy the contents of the `publish` folder to `C:\inetpub\LinkDash` on the server. The output includes the `web.config` that IIS needs.
Your local `App_Data` folder is never published, so a deployment cannot overwrite the links stored on the server.

### 3. Create the site

```powershell
Import-Module WebAdministration

# ASP.NET Core runs its own runtime, so the app pool uses "No Managed Code".
New-WebAppPool -Name LinkDash
Set-ItemProperty IIS:\AppPools\LinkDash -Name managedRuntimeVersion -Value ""

New-Website -Name LinkDash -PhysicalPath C:\inetpub\LinkDash -ApplicationPool LinkDash -Port 8080
```

This serves the app on `http://<server>:8080/`. To use a host name or HTTPS instead, edit the site's bindings in IIS Manager.
The app redirects HTTP to HTTPS once the site has an HTTPS binding; with only an HTTP binding it stays on HTTP.

The app can also run as an application under an existing site (for example `http://<server>/linkdash`): in IIS Manager, right-click the site → *Add Application*, and pick the `LinkDash` app pool.

### 4. Give the app pool write access to the data folder

The app saves groups and links to `App_Data\links.json` inside the site folder. The app pool identity needs permission to create and replace that file:

```powershell
New-Item -ItemType Directory -Force C:\inetpub\LinkDash\App_Data
icacls C:\inetpub\LinkDash\App_Data /grant "IIS AppPool\LinkDash:(OI)(CI)M"
```

To keep the data elsewhere (for example on a backed-up drive), grant the same permission on that folder and point `LinkDash:DataFilePath` at it, for example in an `appsettings.Production.json` that you create on the server next to `appsettings.json`:

```json
{
  "LinkDash": {
    "DataFilePath": "D:\\LinkDashData\\links.json"
  }
}
```

Back up `links.json` regularly; it holds all your groups and links.

To move existing links to the server, copy your local `src/Bergdahl.LinkDash.Web/App_Data/links.json` to the server's data folder.

### 5. Updating to a new version

IIS locks the app's files while it runs. To update:

1. Put a file named `app_offline.htm` in `C:\inetpub\LinkDash`. IIS stops the app and shows that page to visitors.
2. Copy the new `publish` output over the old files.
3. Delete `app_offline.htm`.

Don't delete `App_Data` or your `appsettings.Production.json` when you copy.

### Creating the administrator account

Right after the first deployment, open `http://<server>:8080/Settings` and create the administrator account (see [Signing in](#signing-in)).
It is saved in `App_Data\admin.json`, and the cookie keys in `App_Data\keys`; both use the write access from step 4. Don't delete them when you update.

To set the account through configuration instead, run `dotnet Bergdahl.LinkDash.Web.dll hash-password` in `C:\inetpub\LinkDash` and put the hash in `LinkDash:Admin:PasswordHash` in your `appsettings.Production.json`.

### Troubleshooting

| Error | Likely cause |
| --- | --- |
| HTTP 403 on `/`, and files such as `/appsettings.json` can be downloaded | The site's physical path is not the publish output, so there is no `web.config` and IIS serves the folder as static files. Point the site at the folder that contains `web.config` and `wwwroot` (for example `bin\Release\net10.0\publish`, not `bin\Release\net10.0`). |
| HTTP 500 with an empty page | IIS hides error details from remote browsers. Open the site in a browser on the server to see the exact code below. |
| HTTP 500.19 | The Hosting Bundle is missing, or IIS was not restarted after installing it. |
| HTTP 500.30 / 500.31 | The app failed to start, often because the .NET 10 runtime is missing. Check *Event Viewer* → *Windows Logs* → *Application*. |
| "Access to the path is denied" when saving | The app pool cannot write to the data folder (step 4). |

For more detail, set `stdoutLogEnabled="true"` in `web.config`, create a `logs` folder in the site folder, give the app pool write access to it, and look at the log files. Turn it off again afterwards.
