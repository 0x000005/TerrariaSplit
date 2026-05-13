# TerrariaSplit Intro Animation Design

## Style Prompt

Technical gaming HUD with Terraria speedrun energy: dark metal canvas, pixel-adjacent boss imagery, precise timer typography, scan lines, telemetry panels, and mechanical block transitions. The visual system should feel like a lightweight live overlay rather than a marketing landing page.

## Colors

- Void canvas: `#071113`
- UI text: `#EAF2E8`
- Split gold: `#F4A340`
- Signal teal: `#35C7A6`
- Panel steel: `#27383B`

## Typography

- Display: `Bebas Neue`, with `Noto Sans Japanese` fallback for CJK glyphs
- Data/body: `IBM Plex Mono`, with `Noto Sans Japanese` fallback

## Motion

- Fast HUD entrances, mechanical block transitions, short scan-line motion, no jump cuts.
- Timer, route, and diagnostics elements should arrive as operational UI, not decorative cards.
- Motion must be deterministic and seekable.

## What NOT to Do

- No bright generic gradients.
- No soft one-note purple/blue SaaS palette.
- No random animation order or time-based logic.
- No rounded marketing-card layout.
