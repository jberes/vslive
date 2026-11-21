Build a Design System from the Ignite UI Web Components Storybook
(https://igniteui.github.io/igniteui-webcomponents/), Bootstrap theme,
Light and Dark.

Sources (use exact versions, nothing published in the last 2 weeks):
- igniteui-webcomponents 7.4.x for tokens, theme CSS and components
- igniteui-react (matching version) for the React components
- igniteui-grid-lite for the data grid (React version from igniteui-react/grid-lite)
Pull exact token values from the theme CSS, resolved in a browser. Never guess values.

Components must show by name in the Claude Design properties panel:
1. Ship ONE bundle script that exposes every igniteui-react component on
   window.IgniteUI WITHOUT the Igr prefix (IgniteUI.Button, IgniteUI.Chip,
   IgniteUI.GridLite, IgniteUI.CardHeader...).
2. The bundle must use the page's React 18, not its own copy: point
   react, react-dom and react/jsx-runtime at window.React and window.ReactDOM.
3. List react and react-dom 18.3.1 as the system's libraries.
4. Start the bundle with a @ds-bundle header that lists every component name,
   matching the component card names exactly.
5. Write components/index.d.ts with a <Name>Props interface for each
   component: camelCase props, on<Event> handlers, and allowed values
   written out (e.g. 'primary' | 'success'), not unresolved type names.
6. The bundle also registers the web components, applies the Bootstrap
   theme, and exposes helpers (registerIconFromText, configureTheme, html).
7. bundle.css is the Bootstrap theme with each palette color pointing at
   the system's color tokens, so token edits recolor the components.

Each component card: a live preview, guidelines, a props table, and an
x-import example for the Design canvas.

When building prototypes on a Design canvas from this system:
- Mount every Ignite UI component with
  <x-import component-from-global-scope="IgniteUI.<Name>">, never raw igc- tags.
- Booleans and data go through {{ }} holes (selected="{{isOn}}", data="{{rows}}").
- Events are written on-click, on-input and so on.
- Grid Lite cell templates are functions that return React elements.