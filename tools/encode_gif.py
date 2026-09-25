"""Encodes a folder of PNG frames into a small looping GIF.

Usage: python encode_gif.py <frames_dir> <out.gif> <frame_ms>

One shared palette and no dithering keep unchanged pixels identical between frames,
so the GIF stores only the small regions that actually change.
"""
import glob
import os
import sys

from PIL import Image


def main():
    frames_dir, out, frame_ms = sys.argv[1], sys.argv[2], int(sys.argv[3])
    frames = [Image.open(f).convert("RGB") for f in sorted(glob.glob(os.path.join(frames_dir, "*.png")))]

    # Palette from a strip of frames spread across the animation.
    picks = frames[:: max(1, len(frames) // 6)]
    w, h = frames[0].size
    strip = Image.new("RGB", (w, h * len(picks)))
    for i, f in enumerate(picks):
        strip.paste(f, (0, h * i))
    palette = strip.quantize(160, method=Image.Quantize.MEDIANCUT)

    quantized = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
    quantized[0].save(out, save_all=True, append_images=quantized[1:], duration=frame_ms, loop=0, disposal=1)


if __name__ == "__main__":
    main()
