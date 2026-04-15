#!/usr/bin/env python3
import argparse
import json
from pathlib import Path
from typing import List, Tuple

import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from matplotlib.collections import LineCollection


COCO19_EDGES = [
    (0, 1),
    (1, 2),
    (2, 3),
    (3, 4),
    (2, 5),
    (5, 6),
    (6, 7),
    (7, 8),
    (2, 9),
    (9, 10),
    (10, 11),
    (11, 12),
    (0, 13),
    (13, 14),
    (14, 15),
    (0, 16),
    (16, 17),
    (17, 18),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Replay keypoint frames exported as frame_XXXX.json"
    )
    parser.add_argument(
        "--run-dir",
        required=True,
        help="Directory containing frame_XXXX.json files",
    )
    parser.add_argument(
        "--fps",
        type=float,
        default=30.0,
        help="Playback FPS (default: 30)",
    )
    parser.add_argument(
        "--width",
        type=float,
        default=0.0,
        help="Canvas width in pixels. Auto infer when <= 0",
    )
    parser.add_argument(
        "--height",
        type=float,
        default=0.0,
        help="Canvas height in pixels. Auto infer when <= 0",
    )
    parser.add_argument(
        "--max-frames",
        type=int,
        default=0,
        help="Limit number of frames for quick preview. 0 means all",
    )
    parser.add_argument(
        "--save",
        default="",
        help="Optional output path (.gif/.mp4). If empty, only interactive show",
    )
    parser.add_argument(
        "--no-show",
        action="store_true",
        help="Do not open interactive window (useful when only exporting)",
    )
    parser.add_argument(
        "--point-size",
        type=float,
        default=25.0,
        help="Scatter point size",
    )
    parser.add_argument(
        "--invert-y",
        action="store_true",
        help="Invert Y axis (off by default for Unity screen-space exports)",
    )
    parser.add_argument(
        "--hide-skeleton",
        action="store_true",
        help="Only draw points without skeleton links",
    )
    return parser.parse_args()


def list_frame_files(run_dir: Path, max_frames: int) -> List[Path]:
    files = sorted(run_dir.glob("frame_*.json"))
    if max_frames > 0:
        files = files[:max_frames]
    return files


def read_keypoints(path: Path) -> List[float]:
    with path.open("r", encoding="utf-8") as f:
        obj = json.load(f)
    kp = obj.get("keypoints", [])
    if not isinstance(kp, list):
        return []
    return kp


def to_xyv(kp: List[float]) -> Tuple[List[float], List[float], List[float]]:
    xs: List[float] = []
    ys: List[float] = []
    vs: List[float] = []
    for i in range(0, len(kp), 3):
        if i + 2 >= len(kp):
            break
        x = float(kp[i])
        y = float(kp[i + 1])
        v = float(kp[i + 2])
        xs.append(x)
        ys.append(y)
        vs.append(v)
    return xs, ys, vs


def infer_bounds(
    frame_keypoints: List[List[float]], width: float, height: float
) -> Tuple[float, float, float, float]:
    min_x = float("inf")
    max_x = float("-inf")
    min_y = float("inf")
    max_y = float("-inf")

    for kp in frame_keypoints:
        xs, ys, _ = to_xyv(kp)
        if xs:
            min_x = min(min_x, min(xs))
            max_x = max(max_x, max(xs))
        if ys:
            min_y = min(min_y, min(ys))
            max_y = max(max_y, max(ys))

    if min_x == float("inf") or min_y == float("inf"):
        min_x, max_x = 0.0, max(width, 1.0)
        min_y, max_y = 0.0, max(height, 1.0)

    margin = 50.0
    x_min = 0.0 if width > 0 else min_x - margin
    x_max = width if width > 0 else max_x + margin
    y_min = 0.0 if height > 0 else min_y - margin
    y_max = height if height > 0 else max_y + margin

    if x_max <= x_min:
        x_max = x_min + 1.0
    if y_max <= y_min:
        y_max = y_min + 1.0

    return x_min, x_max, y_min, y_max


def main() -> None:
    args = parse_args()
    run_dir = Path(args.run_dir)
    if not run_dir.exists():
        raise SystemExit(f"run dir not found: {run_dir}")

    frame_files = list_frame_files(run_dir, args.max_frames)
    if not frame_files:
        raise SystemExit(f"no frame_*.json found in: {run_dir}")

    frame_keypoints = [read_keypoints(p) for p in frame_files]
    x_min, x_max, y_min, y_max = infer_bounds(frame_keypoints, args.width, args.height)
    span_x = x_max - x_min
    span_y = y_max - y_min

    fig, ax = plt.subplots(figsize=(max(span_x / 200.0, 6.0), max(span_y / 200.0, 4.0)), dpi=120)
    ax.set_facecolor("#121417")
    fig.patch.set_facecolor("#121417")
    ax.set_xlim(x_min, x_max)
    ax.set_ylim(y_min, y_max)
    if args.invert_y:
        ax.invert_yaxis()
    ax.set_xlabel("x")
    ax.set_ylabel("y")
    ax.set_title(f"Replay: {run_dir.name}")

    scatter = ax.scatter([], [], s=args.point_size, c="#00d4ff")
    skeleton = LineCollection([], colors="#8e56ff", linewidths=2.0, alpha=0.9)
    if not args.hide_skeleton:
        ax.add_collection(skeleton)

    frame_text = ax.text(
        0.02,
        0.03,
        "",
        transform=ax.transAxes,
        color="#e7edf3",
        fontsize=10,
        bbox=dict(facecolor="#1f242a", alpha=0.7, edgecolor="none", boxstyle="round,pad=0.25"),
    )

    def update(frame_idx: int):
        kp = frame_keypoints[frame_idx]
        xs, ys, vs = to_xyv(kp)

        visible_points = [(idx, x, y) for idx, (x, y, v) in enumerate(zip(xs, ys, vs)) if v > 0]
        points = [(x, y) for _, x, y in visible_points]
        if points:
            scatter.set_offsets(points)
        else:
            scatter.set_offsets([])

        if not args.hide_skeleton:
            by_idx = {idx: (x, y) for idx, x, y in visible_points}
            segments = []
            for a, b in COCO19_EDGES:
                pa = by_idx.get(a)
                pb = by_idx.get(b)
                if pa is not None and pb is not None:
                    segments.append([pa, pb])
            skeleton.set_segments(segments)
        else:
            skeleton.set_segments([])

        frame_text.set_text(
            f"frame: {frame_idx + 1}/{len(frame_files)}\n"
            f"file: {frame_files[frame_idx].name}\n"
            f"points: {len(points)}"
        )
        return scatter, frame_text, skeleton

    interval_ms = 1000.0 / max(args.fps, 1e-6)
    anim = FuncAnimation(fig, update, frames=len(frame_files), interval=interval_ms, blit=False, repeat=True)

    if args.save:
        save_path = Path(args.save)
        save_path.parent.mkdir(parents=True, exist_ok=True)
        suffix = save_path.suffix.lower()

        if suffix == ".gif":
            anim.save(save_path, writer="pillow", fps=args.fps)
        elif suffix == ".mp4":
            anim.save(save_path, writer="ffmpeg", fps=args.fps)
        else:
            raise SystemExit("--save only supports .gif or .mp4")
        print(f"saved replay: {save_path}")

    if not args.no_show:
        plt.show()


if __name__ == "__main__":
    main()
