# Blazor Error Banner CSS Kludge

## Summary

A Blazor Server circuit/render error can cause the built-in error UI banner to appear at the bottom of the page:

"An unhandled error has occurred. Reload 🗙"

As a temporary UI-only workaround, the banner can be hidden with CSS.

## Kludge

Update `wwwroot/app.css`:

```css
#blazor-error-ui {
    display: none !important;
}
```

## Notes

- This only hides the banner; it does not fix the underlying circuit/server error.
- Use this when the banner is disruptive for end users while you continue to diagnose the root cause.
