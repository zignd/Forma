# API Compatibility Report

The Phase 0 baseline is the public `Microsoft.Xna.Framework.UI` surface compiled from clean
`zignd/MonoGame` revision `49ea4f3d4a7e3638a9ed0875469dcd6f5af6000f`. The UI was introduced in
commit `35921960e8d8210bcd01476a54e8cb5d03895e1d`. `PublicApiGenerator` 11.5.4 records all public
types and members, with only the root namespace normalized to `Forma` for comparison.

| Inventory | Top-level types | Declaration lines |
| --- | ---: | ---: |
| Phase 0 normalized baseline | 185 | 3,615 |
| Approved stock-compatible Forma core | 184 | 3,567 |

The deterministic report in `docs/api-compatibility.diff` contains one intentional difference:
`VideoStreamPlayer` and its members are absent from the stock-compatible core and live in the
optional `Forma.Media` assembly. Its original public surface is preserved there. The media assembly
also adds the `IVideoPlaybackBackend` integration interface and one injection constructor. There are
no other type, member, visibility, inheritance, signature, enum-value, or attribute differences.

Run `bash scripts/check-api-compatibility.sh` to build Forma, regenerate its current API inventory,
compare it with `docs/api-core.approved.txt`, compare Forma.Media with
`docs/api-media.approved.txt`, and verify that the baseline delta has not changed.