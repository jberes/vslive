# Demo 4: Build a design system from Ignite UI

This demo has no app code. It is a single prompt, [`Build a Design System from the Ignite UI.md`](Build%20a%20Design%20System%20from%20the%20Ignite%20UI.md), that asks **Claude Design** to build a reusable **design system** from the [Ignite UI Web Components Storybook](https://igniteui.github.io/igniteui-webcomponents/), using the **Bootstrap theme in light and dark**.

## What the prompt asks for

- **Sources pinned to exact versions:** `igniteui-webcomponents` 7.4.x (tokens, theme CSS and components), the matching `igniteui-react`, and `igniteui-grid-lite` for the data grid. Token values are read from the resolved theme CSS in a browser, never guessed.
- **One bundle script** that puts every `igniteui-react` component on `window.IgniteUI` without the `Igr` prefix (`IgniteUI.Button`, `IgniteUI.GridLite`, …). It uses the page's own React 18.3.1 and starts with a `@ds-bundle` header that lists every component.
- **`components/index.d.ts`** with a `<Name>Props` interface for each component (camelCase props, `on<Event>` handlers, and allowed values written out as literals), so the components show up by name in the Claude Design properties panel.
- **`bundle.css`**: the Bootstrap theme with each palette color pointing at the system's color tokens, so editing a token recolors the components.
- **A card for each component** with a live preview, guidelines, a props table and an `x-import` example.
- **Rules for building prototypes** on a Design canvas: mount components with `<x-import component-from-global-scope="IgniteUI.<Name>">`, pass booleans and data through `{{ }}` holes, write events as `on-click`, and write Grid Lite cell templates as functions that return React elements.

## How to use it

1. Open [Claude Design](https://claude.ai) and start a new design system.
2. Paste the contents of [`Build a Design System from the Ignite UI.md`](Build%20a%20Design%20System%20from%20the%20Ignite%20UI.md) as the prompt.
3. When it's done, check the component cards (live previews, props tables), then try a prototype on a Design canvas that uses `x-import` components.

There is nothing to install or run locally for this demo.
