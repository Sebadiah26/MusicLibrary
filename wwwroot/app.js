// Ensure global `$` refers to jQuery if present (avoids BrowserLink $.noConflict error)
if (typeof window !== "undefined" && window.jQuery && (!window.$ || typeof window.$.noConflict !== "function")) {
  // Assign window.jQuery to window.$ so scripts that call $.noConflict() see the jQuery API.
  // This is conservative: we only overwrite window.$ when it is missing or doesn't have noConflict.
  window.$ = window.jQuery;
}

// selector helper — do not clobber global `$` (avoids jQuery / BrowserLink conflicts)
const $sel = (sel) => document.querySelector(sel);

const els = {
  type: $sel("#csvType"),
  file: $sel("#csvFile"),
  uploadBtn: $sel("#uploadBtn"),
  uploadStatus: $sel("#uploadStatus"),
  itunesFile: $sel("#itunesFile"),
  itunesImportBtn: $sel("#itunesImportBtn"),
  itunesConvertBtn: $sel("#itunesConvertBtn"),
  itunesStatus: $sel("#itunesStatus"),
  search: $sel("#search"),
  genreFilter: $sel("#genreFilter"),
  subGenreFilter: $sel("#subGenreFilter"),
  favOnly: $sel("#favOnly"),
  viewToggleBtn: $sel("#viewToggleBtn"),
  refreshBtn: $sel("#refreshBtn"),
  clearAllBtn: $sel("#clearAllBtn"),
  artists: $sel("#artists"),
  genreView: $sel("#genreView"),
  emptyMsg: $sel("#emptyMsg"),
};

// ---- Upload ----------------------------------------------------------------

els.uploadBtn.addEventListener("click", async () => {
  const file = els.file.files[0];
  if (!file) {
    showStatus("Choose a CSV file first.", false);
    return;
  }

  const form = new FormData();
  form.append("file", file);
  form.append("type", els.type.value);

  els.uploadBtn.disabled = true;
  els.uploadBtn.textContent = "Uploading…";

  try {
    const res = await fetch("/api/upload", { method: "POST", body: form });
    const data = await res.json();
    if (res.ok) {
      showStatus((data.messages || ["Imported."]).join(" "), true);
      els.file.value = "";
      await loadArtists();
    } else {
      showStatus(data.error || (data.messages || []).join(" ") || "Upload failed.", false);
    }
  } catch (err) {
    showStatus("Network error: " + err.message, false);
  } finally {
    els.uploadBtn.disabled = false;
    els.uploadBtn.textContent = "Upload";
  }
});

function showStatus(msg, ok) {
  els.uploadStatus.textContent = msg;
  els.uploadStatus.className = "status " + (ok ? "ok" : "err");
  els.uploadStatus.hidden = false;
}

// ---- iTunes XML Import -----------------------------------------------------

function showItunesStatus(msg, ok) {
  els.itunesStatus.textContent = msg;
  els.itunesStatus.className = "status " + (ok ? "ok" : "err");
  els.itunesStatus.hidden = false;
}

els.itunesImportBtn.addEventListener("click", async () => {
  const file = els.itunesFile.files[0];
  if (!file) {
    showItunesStatus("Choose an iTunes XML file first.", false);
    return;
  }

  const form = new FormData();
  form.append("file", file);

  els.itunesImportBtn.disabled = true;
  els.itunesConvertBtn.disabled = true;
  els.itunesImportBtn.textContent = "Importing…";

  try {
    const res = await fetch("/api/itunes/import", { method: "POST", body: form });
    const data = await res.json();
    if (res.ok) {
      showItunesStatus((data.messages || ["Imported."]).join(" "), true);
      els.itunesFile.value = "";
      await loadArtists();
    } else {
      showItunesStatus(data.error || "Import failed.", false);
    }
  } catch (err) {
    showItunesStatus("Network error: " + err.message, false);
  } finally {
    els.itunesImportBtn.disabled = false;
    els.itunesConvertBtn.disabled = false;
    els.itunesImportBtn.textContent = "Import to Library";
  }
});

els.itunesConvertBtn.addEventListener("click", async () => {
  const file = els.itunesFile.files[0];
  if (!file) {
    showItunesStatus("Choose an iTunes XML file first.", false);
    return;
  }

  const form = new FormData();
  form.append("file", file);

  els.itunesImportBtn.disabled = true;
  els.itunesConvertBtn.disabled = true;
  els.itunesConvertBtn.textContent = "Converting…";

  try {
    const res = await fetch("/api/itunes/convert", { method: "POST", body: form });
    const data = await res.json();
    if (res.ok) {
      downloadCsvFile("artists.csv", data.artistsCsv);
      downloadCsvFile("albums.csv", data.albumsCsv);
      downloadCsvFile("songs.csv", data.songsCsv);
      showItunesStatus(`Converted ${data.trackCount} track(s). Three CSV files downloaded.`, true);
    } else {
      showItunesStatus(data.error || "Conversion failed.", false);
    }
  } catch (err) {
    showItunesStatus("Network error: " + err.message, false);
  } finally {
    els.itunesImportBtn.disabled = false;
    els.itunesConvertBtn.disabled = false;
    els.itunesConvertBtn.textContent = "Download as CSV";
  }
});

function downloadCsvFile(filename, content) {
  const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(link.href);
}

// ---- List ------------------------------------------------------------------

let debounce;
els.search.addEventListener("input", () => {
  clearTimeout(debounce);
  debounce = setTimeout(loadArtists, 250);
});
els.favOnly.addEventListener("change", loadArtists);
els.genreFilter.addEventListener("change", () => {
  updateSubGenreOptions();
  loadArtists();
});
els.subGenreFilter.addEventListener("change", loadArtists);
els.refreshBtn.addEventListener("click", () => { loadGenres(); loadArtists(); });
let viewMode = "list"; // "list" or "genre"
els.viewToggleBtn.addEventListener("click", () => {
  viewMode = viewMode === "list" ? "genre" : "list";
  els.viewToggleBtn.textContent = viewMode === "list" ? "By Genre" : "Detail View";
  loadArtists();
});

els.clearAllBtn.addEventListener("click", async () => {
  if (!confirm("Delete ALL artists, albums, and songs? This cannot be undone.")) return;
  els.clearAllBtn.disabled = true;
  els.clearAllBtn.textContent = "Clearing…";
  try {
    const res = await fetch("/api/reset", { method: "DELETE" });
    if (res.ok) await loadArtists();
    else alert("Failed to clear data.");
  } catch (err) {
    alert("Network error: " + err.message);
  }
  els.clearAllBtn.disabled = false;
  els.clearAllBtn.textContent = "Clear All Data";
});

let genreData = [];

async function loadGenres() {
  const res = await fetch("/api/genres");
  genreData = await res.json();
  const selected = els.genreFilter.value;
  els.genreFilter.innerHTML = '<option value="">All genres</option>';
  for (const g of genreData) {
    const opt = document.createElement("option");
    opt.value = g.genre;
    opt.textContent = g.genre;
    els.genreFilter.appendChild(opt);
  }
  els.genreFilter.value = selected;
  updateSubGenreOptions();
}

function updateSubGenreOptions() {
  const genre = els.genreFilter.value;
  const selected = els.subGenreFilter.value;
  els.subGenreFilter.innerHTML = '<option value="">All sub-genres</option>';
  if (!genre) { els.subGenreFilter.disabled = true; return; }
  const entry = genreData.find(g => g.genre === genre);
  if (!entry || entry.subGenres.length === 0) { els.subGenreFilter.disabled = true; return; }
  els.subGenreFilter.disabled = false;
  for (const s of entry.subGenres) {
    const opt = document.createElement("option");
    opt.value = s;
    opt.textContent = s;
    els.subGenreFilter.appendChild(opt);
  }
  els.subGenreFilter.value = selected;
}

async function loadArtists() {
  const params = new URLSearchParams();
  if (els.search.value.trim()) params.set("search", els.search.value.trim());
  if (els.favOnly.checked) params.set("favorites", "true");
  if (els.genreFilter.value) params.set("genre", els.genreFilter.value);
  if (els.subGenreFilter.value) params.set("subGenre", els.subGenreFilter.value);

  const res = await fetch("/api/artists?" + params.toString());
  const list = await res.json();

  els.artists.innerHTML = "";
  els.genreView.innerHTML = "";
  els.emptyMsg.hidden = list.length > 0;

  if (viewMode === "genre") {
    els.artists.hidden = true;
    els.genreView.hidden = false;
    renderGenreView(list);
  } else {
    els.artists.hidden = false;
    els.genreView.hidden = true;
    for (const a of list) {
      els.artists.appendChild(renderArtist(a));
    }
  }
}

function renderGenreView(list) {
  // Group by genre, then sub-genre
  const groups = {};
  for (const a of list) {
    const genre = a.genre || "(No genre)";
    const sub = a.subGenre || "";
    if (!groups[genre]) groups[genre] = {};
    if (!groups[genre][sub]) groups[genre][sub] = [];
    groups[genre][sub].push(a.name);
  }

  const sortedGenres = Object.keys(groups).sort((a, b) => {
    if (a === "(No genre)") return 1;
    if (b === "(No genre)") return -1;
    return a.localeCompare(b);
  });

  for (const genre of sortedGenres) {
    const section = document.createElement("div");
    section.className = "genre-section";

    const subs = groups[genre];
    const subKeys = Object.keys(subs).sort((a, b) => {
      if (a === "") return 1;
      if (b === "") return -1;
      return a.localeCompare(b);
    });

    // If there's only one sub-genre group (empty string), show flat under genre header
    const hasSubGenres = subKeys.length > 1 || (subKeys.length === 1 && subKeys[0] !== "");

    const h3 = document.createElement("h3");
    h3.textContent = genre;
    section.appendChild(h3);

    for (const sub of subKeys) {
      const names = subs[sub].sort((a, b) => a.localeCompare(b));

      if (hasSubGenres && sub) {
        const group = document.createElement("div");
        group.className = "subgenre-group";
        const h4 = document.createElement("h4");
        h4.textContent = sub;
        group.appendChild(h4);
        group.appendChild(renderArtistColumns(names));
        section.appendChild(group);
      } else if (hasSubGenres && !sub) {
        // Artists with no sub-genre in a genre that has sub-genres
        const group = document.createElement("div");
        group.className = "subgenre-group";
        const h4 = document.createElement("h4");
        h4.textContent = "(No sub-genre)";
        group.appendChild(h4);
        group.appendChild(renderArtistColumns(names));
        section.appendChild(group);
      } else {
        section.appendChild(renderArtistColumns(names));
      }
    }

    els.genreView.appendChild(section);
  }
}

function renderArtistColumns(names) {
  const div = document.createElement("div");
  div.className = "artist-columns";
  for (const name of names) {
    const span = document.createElement("span");
    span.textContent = name;
    div.appendChild(span);
  }
  return div;
}

function renderArtist(a) {
  const wrap = document.createElement("div");
  wrap.className = "artist";

  const favPart = a.favoriteSongCount > 0 ? ` · ${a.favoriteSongCount} fav${a.favoriteSongCount === 1 ? "" : "s"}` : "";
  const counts = `${a.albumCount} album${a.albumCount === 1 ? "" : "s"} · ${a.songCount} song${a.songCount === 1 ? "" : "s"}${favPart}`;

  const main = document.createElement("div");
  main.className = "artist-main";
  main.innerHTML = `
    <div class="artist-name">${escapeHtml(a.name)}<span class="counts">${counts}</span></div>
    <div class="cell">
      <span class="field-label">Genre</span>
      <input type="text" class="genre" value="${escapeAttr(a.genre)}" placeholder="—" />
    </div>
    <div class="cell">
      <span class="field-label">Sub-genre</span>
      <input type="text" class="subgenre" value="${escapeAttr(a.subGenre)}" placeholder="—" />
    </div>
    <div class="cell">
      <span class="field-label">Favorite</span>
      <button class="fav ${a.isFavorite ? "on" : ""}" title="Toggle favorite">♥</button>
    </div>
    <div class="cell">
      <span class="field-label">Rating</span>
      <div class="stars"></div>
    </div>
    <div class="row-actions">
      <button class="small save">Save</button>
    </div>
    <div class="row-actions">
      <button class="small ghost details">Details</button>
      <button class="small ghost del">Delete</button>
    </div>
  `;

  // Favorite toggle (auto-saves)
  const favBtn = main.querySelector(".fav");
  let isFav = a.isFavorite;
  favBtn.addEventListener("click", async () => {
    isFav = !isFav;
    favBtn.classList.toggle("on", isFav);
    await fetch(`/api/artists/${a.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ genre: main.querySelector(".genre").value, subGenre: main.querySelector(".subgenre").value, isFavorite: isFav, rating: rating === 0 ? null : rating }),
    });
  });

  // Star rating
  const starsEl = main.querySelector(".stars");
  let rating = a.rating || 0;
  function drawStars() {
    starsEl.innerHTML = "";
    for (let i = 1; i <= 5; i++) {
      const s = document.createElement("span");
      s.className = "star" + (i <= rating ? " on" : "");
      s.textContent = "★";
      s.addEventListener("click", () => { rating = i; drawStars(); });
      starsEl.appendChild(s);
    }
    const clear = document.createElement("span");
    clear.className = "clear";
    clear.textContent = "clear";
    clear.addEventListener("click", () => { rating = 0; drawStars(); });
    starsEl.appendChild(clear);
  }
  drawStars();

  // Save
  main.querySelector(".save").addEventListener("click", async (e) => {
    const btn = e.target;
    btn.disabled = true;
    btn.textContent = "Saving…";
    const body = {
      genre: main.querySelector(".genre").value,
      subGenre: main.querySelector(".subgenre").value,
      isFavorite: isFav,
      rating: rating === 0 ? null : rating,
    };
    try {
      const res = await fetch(`/api/artists/${a.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        alert(d.error || "Save failed.");
      } else {
        btn.textContent = "Saved ✓";
        setTimeout(() => { btn.textContent = "Save"; btn.disabled = false; }, 1200);
        return;
      }
    } catch (err) {
      alert("Network error: " + err.message);
    }
    btn.textContent = "Save";
    btn.disabled = false;
  });

  // Delete
  main.querySelector(".del").addEventListener("click", async () => {
    if (!confirm(`Delete "${a.name}" and all its albums/songs?`)) return;
    const res = await fetch(`/api/artists/${a.id}`, { method: "DELETE" });
    if (res.ok) wrap.remove();
    else alert("Delete failed.");
  });

  // Details (lazy load)
  const detail = document.createElement("div");
  detail.className = "detail";
  let loaded = false;
  main.querySelector(".details").addEventListener("click", async () => {
    detail.classList.toggle("open");
    if (!detail.classList.contains("open") || loaded) return;
    loaded = true;
    detail.innerHTML = "Loading…";
    const res = await fetch(`/api/artists/${a.id}`);
    const d = await res.json();
    detail.innerHTML = renderDetail(d);
  });

  wrap.appendChild(main);
  wrap.appendChild(detail);
  return wrap;
}

function renderDetail(d) {
  const albums = (d.albums || []).map(al =>
    `<li>${escapeHtml(al.title)}${al.year ? ` (${al.year})` : ""} — ${al.songCount} song${al.songCount === 1 ? "" : "s"}</li>`
  ).join("");
  const songs = (d.songs || []).map(s =>
    `<li>${escapeHtml(s.title)}${s.durationSeconds ? ` — ${fmtDuration(s.durationSeconds)}` : ""}</li>`
  ).join("");

  return `
    <h4>Albums (${(d.albums || []).length})</h4>
    ${albums ? `<ul>${albums}</ul>` : "<p class='empty'>No albums.</p>"}
    <h4>Songs (${(d.songs || []).length})</h4>
    ${songs ? `<ul>${songs}</ul>` : "<p class='empty'>No songs.</p>"}
  `;
}

// ---- Helpers ---------------------------------------------------------------

function fmtDuration(sec) {
  const m = Math.floor(sec / 60);
  const s = String(sec % 60).padStart(2, "0");
  return `${m}:${s}`;
}
function escapeHtml(v) {
  return String(v ?? "").replace(/[&<>"']/g, c =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
function escapeAttr(v) {
  return escapeHtml(v).replace(/"/g, "&quot;");
}

loadGenres();
loadArtists();
