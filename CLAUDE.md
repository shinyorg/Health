# Shiny.Health — Working Notes

Guidance for maintaining this repo. Code lives in `src/`, tests in `tests/`, the published Claude
Code skill in `skills/`, and the public documentation site in a **separate** repo at
`~/Desktop/dev/documentation` (rendered to https://shinylib.net/health).

`Shiny.Health` is a unified cross-platform health data API for .NET MAUI over Apple HealthKit and
Android Health Connect.

## Documentation site

The public docs live in a **separate repo**: `~/Desktop/dev/documentation` (Astro / Starlight).

- Feature pages: `src/content/docs/health/*.mdx` (e.g. `reading.mdx`, `writing.mdx`,
  `observing.mdx`, `platform-notes.mdx`).
- Release notes: `src/content/docs/health/release-notes.mdx`.
- Menu (sidebar): `src/sidebar-topics.mjs` — add/update the relevant node when you add a feature page.

### Required updates for EVERY fix & feature

A change is not "done" until these are in sync:

1. **readme.md** (repo root) — packed into the NuGet package; reflect new/changed behavior.
2. **Skill** (`skills/`) — the agent-facing "how to generate correct code" doc; update the trigger
   keyword list when a new public `DataType` / API is introduced.
3. **Docs site** — update the relevant feature page and add a **release note**.

### Release notes

Notes use the `<RN>` component (`import RN from '/src/components/ReleaseNote.astro'`), with
`type="feature|enhancement|fix|chore"`, an optional `breaking` flag, and an optional
`platform="iOS|Android"` for platform-specific notes. Newest version section stays at the top; use
an `### <version> - TBD` heading for unreleased work and promote it to a dated heading
(`### 1.1.0 - June 14, 2026`) when cutting the release.

## Blog posts (only when explicitly requested)

Do **not** write blog posts automatically as part of a fix/feature. Write them **only when the user asks**. When asked to blog a feature, produce **two** posts — first the docs-site version, then adapt it for the personal blog.

### 1. Docs site — `~/Desktop/dev/documentation`

- File: `src/content/docs/blog/YYYY/MM/<slug>.mdx` (current year/month folders; create the month folder if needed).
- Frontmatter:
  ```yaml
  ---
  title: '...'
  description: '...'
  date: YYYY-MM-DD
  authors:
    - allanritchie
  tags:
    - Release        # or Feature, AI, etc.
  ---
  ```
- Body is MDX. Reuse components where relevant, e.g. `import NugetBadge from '/src/components/NugetBadge.astro';` then `<NugetBadge name="Shiny.Health" />`.
- Voice: product/release-note tone — what shipped, breaking changes, code samples, how to use it. **No hero image** on this site.

### 2. Personal blog — `~/Desktop/dev/blog` (adapt the docs post)

- File: `src/content/blog/YYYY/MM/<slug>.mdx` (note: `content/blog`, not `content/docs/blog`).
- Frontmatter (different schema — see `src/content.config.ts`):
  ```yaml
  ---
  title: '...'
  description: '...'
  pubDate: 'Mon DD YYYY'                          # e.g. 'Jun 15 2026'
  heroImage: '../../../../assets/<slug>-hero.svg'
  tags: ['Shiny', '.NET MAUI']
  ---
  ```
- Voice: rework the docs post into a personal, first-person narrative ("Here's something that shouldn't be hard but is…", "So I built…") — story/motivation up front, not a dry changelog.
- **Hero image is required.** Create `src/assets/<slug>-hero.svg`:
  - SVG, `viewBox="0 0 1200 630"`, `width="1200" height="630"`.
  - Match the house style: dark navy/indigo gradient background (`#0f172a` → `#1e1b4b`), cyan/green/violet accent gradients, subtle glow filters, the feature name as the headline. Crib an existing one (e.g. `datasync-hero.svg`, `documentdb-orleans-hero.svg`) as a starting template.
