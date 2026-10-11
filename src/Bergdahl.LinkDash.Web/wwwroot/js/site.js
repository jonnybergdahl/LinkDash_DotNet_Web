// Ask for confirmation before submitting forms marked with data-confirm (delete actions).
document.addEventListener("submit", event => {
    const message = event.target.dataset?.confirm;
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

// Icon value that shows the link's own site icon, fetched and cached by the server (links only).
const faviconIcon = "favicon";

// Show an MDI glyph (e.g. "mdi:home") in a round icon container, replacing a favicon if it had one.
const showGlyph = (container, icon) => {
    container.querySelector("img[data-favicon]")?.remove();
    container.classList.remove("favicon", ...[...container.classList].filter(c => c.startsWith("mdi-")));
    container.classList.add("mdi", `mdi-${icon.replace(/^mdi:/, "")}`);
};

// Favicons the server could not find answer 404; show the default link icon instead of a broken image.
// Listen in the capture phase since error events do not bubble, and catch images that failed before this script ran.
document.addEventListener("error", event => {
    if (event.target instanceof HTMLImageElement && event.target.matches("[data-favicon]")) {
        showGlyph(event.target.parentElement, "mdi:link");
    }
}, true);
document.querySelectorAll("img[data-favicon]").forEach(img => {
    if (img.complete && img.naturalWidth === 0) {
        showGlyph(img.parentElement, "mdi:link");
    }
});

// Live preview of the Material Design Icons icon (e.g. "mdi:home") while typing in the link form.
document.querySelectorAll("input[data-icon-preview]").forEach(input => {
    const preview = document.getElementById(input.dataset.iconPreview);
    const { allowFavicon, faviconUrl, emptyIcon = "mdi:link" } = input.dataset;

    // A new link has no saved site to load the favicon from yet, so a globe stands in for it.
    const showFavicon = () => {
        if (!faviconUrl) {
            showGlyph(preview, "mdi:web");
        } else if (!preview.querySelector("img[data-favicon]")) {
            preview.classList.remove("mdi", ...[...preview.classList].filter(c => c.startsWith("mdi-")));
            preview.classList.add("favicon");
            const img = document.createElement("img");
            img.src = faviconUrl;
            img.alt = "";
            img.dataset.favicon = "";
            preview.append(img);
        }
    };

    const update = () => {
        const value = input.value.trim();
        if (allowFavicon && value === faviconIcon) {
            showFavicon();
        } else {
            showGlyph(preview, /^(mdi:)?[a-z0-9]+(-[a-z0-9]+)*$/.test(value) ? value : emptyIcon);
        }
    };
    input.addEventListener("input", update);
    input.addEventListener("change", update);
});

// Searchable icon dropdown, like the icon picker in Home Assistant.
// The icon index is a list of [name, aliases] pairs, loaded the first time the field is used.
document.querySelectorAll("[data-icon-picker]").forEach(picker => {
    const input = picker.querySelector("input");
    const list = picker.querySelector("[role=listbox]");
    const allowFavicon = input.dataset.allowFavicon !== undefined;
    const maxResults = 100;
    let icons = null;
    let loading = null;
    let results = [];
    let active = -1;

    const loadIcons = () => loading ??= fetch(picker.dataset.iconsUrl)
        .then(response => response.ok ? response.json() : [])
        .catch(() => [])
        .then(data => icons = data);

    // Lower score is a better match; null means no match.
    const score = ([name, aliases = ""], query, terms) => {
        if (name === query) return 0;
        if (name.startsWith(query)) return 1;
        if (name.includes(query)) return 2;
        if (aliases.split(" ").includes(query)) return 3;
        const text = `${name} ${aliases}`;
        return terms.every(term => text.includes(term)) ? 4 : null;
    };

    const search = value => {
        const query = value.trim().toLowerCase().replace(/^mdi:/, "").replace(/\s+/g, "-");
        if (!query) {
            return icons.slice(0, maxResults).map(icon => icon[0]);
        }
        const terms = query.split("-").filter(Boolean);
        return icons
            .map(icon => ({ name: icon[0], score: score(icon, query, terms) }))
            .filter(match => match.score !== null)
            .sort((a, b) => a.score - b.score || a.name.length - b.name.length || a.name.localeCompare(b.name))
            .slice(0, maxResults)
            .map(match => match.name);
    };

    const setActive = index => {
        list.querySelector("[aria-selected=true]")?.setAttribute("aria-selected", "false");
        active = index;
        const option = list.children[index];
        if (option) {
            option.setAttribute("aria-selected", "true");
            option.scrollIntoView({ block: "nearest" });
            input.setAttribute("aria-activedescendant", option.id);
        } else {
            input.removeAttribute("aria-activedescendant");
        }
    };

    const close = () => {
        list.hidden = true;
        input.setAttribute("aria-expanded", "false");
        setActive(-1);
    };

    // In link forms the site's own icon is offered first, while the search is empty or could mean it.
    const matchesFavicon = value => {
        const query = value.trim().toLowerCase();
        return allowFavicon && (!query || ["favicon", "site", "website"].some(word => word.startsWith(query)));
    };

    const render = () => {
        results = [...(matchesFavicon(input.value) ? [faviconIcon] : []), ...search(input.value).map(name => `mdi:${name}`)];
        list.replaceChildren(...results.map((value, i) => {
            const option = document.createElement("li");
            option.id = `icon-option-${i}`;
            option.setAttribute("role", "option");
            option.setAttribute("aria-selected", "false");
            option.dataset.index = i;
            const glyph = document.createElement("span");
            glyph.className = `mdi mdi-${value === faviconIcon ? "web" : value.replace(/^mdi:/, "")}`;
            glyph.setAttribute("aria-hidden", "true");
            option.append(glyph, value === faviconIcon ? `${faviconIcon} (the site's own icon)` : value);
            return option;
        }));
        if (results.length === 0) {
            const empty = document.createElement("li");
            empty.className = "icon-options-empty";
            empty.textContent = "No matching icons";
            list.append(empty);
        }
        list.hidden = false;
        input.setAttribute("aria-expanded", "true");
        setActive(-1);
    };

    const open = async () => {
        await loadIcons();
        if (icons.length > 0 && document.activeElement === input) {
            render();
        }
    };

    const choose = index => {
        input.value = results[index];
        input.dispatchEvent(new Event("change", { bubbles: true }));
        close();
    };

    input.addEventListener("focus", open);
    input.addEventListener("click", () => list.hidden && open());
    input.addEventListener("input", open);
    input.addEventListener("blur", close);

    input.addEventListener("keydown", event => {
        if (list.hidden) {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                open();
            }
            return;
        }
        switch (event.key) {
            case "ArrowDown":
                event.preventDefault();
                setActive(Math.min(active + 1, results.length - 1));
                break;
            case "ArrowUp":
                event.preventDefault();
                setActive(Math.max(active - 1, 0));
                break;
            case "Enter":
                if (active >= 0) {
                    event.preventDefault();
                    choose(active);
                }
                break;
            case "Escape":
                event.preventDefault();
                close();
                break;
        }
    });

    // Keep focus in the input while clicking an option, so blur does not close the list first.
    list.addEventListener("mousedown", event => event.preventDefault());
    list.addEventListener("click", event => {
        const option = event.target.closest("[role=option]");
        if (option) {
            choose(Number(option.dataset.index));
        }
    });
});

// Hide the snackbar after a few seconds.
const snackbar = document.querySelector(".snackbar");
if (snackbar) {
    setTimeout(() => {
        snackbar.classList.add("hide");
        setTimeout(() => snackbar.remove(), 300);
    }, 4000);
}
