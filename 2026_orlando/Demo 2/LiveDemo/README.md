# LiveDemo

This folder holds the React app built during the live demo, plus a small npm helper used to create it.

| Path | What it is |
| --- | --- |
| [`livedemo1/`](livedemo1/README.md) | The app: "Healthy", a React + Vite + Ignite UI for React health and wellness tracker. Start here. |
| `package.json`, `package-lock.json` | A scaffolding helper, not an app. |

## What `package.json` in this folder is

`LiveDemo/package.json` has no name, no scripts and a single dependency:

```json
{
  "dependencies": {
    "igniteui-cli": "^16.0.0"
  }
}
```

It installs the [Ignite UI CLI](https://github.com/IgniteUI/igniteui-cli) (locked to 16.0.0 in `package-lock.json`) locally, so its `ig` command is available in this folder without a global install. `livedemo1` was generated with Ignite UI CLI 16.0.0 (see `livedemo1/ignite-ui-cli.json`).

It is **not** an npm workspace root: it has no `workspaces` field, and `livedemo1` has its own `package.json` and `package-lock.json`. You do not need to install anything here to run the app.

## Running the app

```sh
cd livedemo1
npm install
npm start
```

See [livedemo1/README.md](livedemo1/README.md) for prerequisites, scripts and troubleshooting.

## Optional: using the CLI helper

If you want to repeat the scaffolding step, install the helper and run the CLI through `npx`:

```sh
npm install
npx ig help   # list the available commands
npx ig        # interactive step-by-step mode for creating a new project
```
 `livedemo1` also lists `igniteui-cli` (~16.0.0) as a dev dependency, so inside that folder you can run `npx ig` after its own `npm install`.
