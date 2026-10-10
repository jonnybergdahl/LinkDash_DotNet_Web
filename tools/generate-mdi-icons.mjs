// Builds the icon index used by the icon picker from the @mdi/svg metadata.
// Keep the version in sync with the @mdi/font stylesheet in _Layout.cshtml.
//
//   node tools/generate-mdi-icons.mjs 7.4.47
import { writeFileSync } from "node:fs";

const version = process.argv[2] ?? "7.4.47";
const output = new URL("../src/Bergdahl.LinkDash.Web/wwwroot/lib/mdi/icons.json", import.meta.url);

const response = await fetch(`https://cdn.jsdelivr.net/npm/@mdi/svg@${version}/meta.json`);
if (!response.ok) {
    throw new Error(`Could not download MDI ${version} metadata: ${response.status}`);
}

// Deprecated icons (mostly brand logos like "github") are still in the font, so they are kept.
const icons = (await response.json())
    .map(icon => icon.aliases.length ? [icon.name, icon.aliases.join(" ")] : [icon.name])
    .sort((a, b) => a[0].localeCompare(b[0]));

writeFileSync(output, JSON.stringify(icons));
console.log(`Wrote ${icons.length} icons from MDI ${version}.`);
