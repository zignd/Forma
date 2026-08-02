# Video Test Assets

`forma-video-smoke.ogv` is a repository-owned deterministic color test pattern with a generated
440 Hz sine track. It verifies FNA Theora/Vorbis decoding, visible frame changes, playback state,
looping, and completion. It contains no third-party visual or audio material.

Generate it with FFmpeg 7 and libtheora:

```sh
ffmpeg -y \
  -f lavfi -i "testsrc2=size=64x64:rate=12:duration=1" \
  -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=1" \
  -map 0:v:0 -map 1:a:0 \
  -c:v libtheora -q:v 7 -pix_fmt yuv420p \
  -c:a libvorbis -q:a 4 -shortest \
  -map_metadata -1 -fflags +bitexact -flags:v +bitexact -flags:a +bitexact \
  -serial_offset 0 forma-video-smoke.ogv
```