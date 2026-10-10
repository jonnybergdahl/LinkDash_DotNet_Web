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

> The settings page has no authentication. Only expose the app on networks you trust.
