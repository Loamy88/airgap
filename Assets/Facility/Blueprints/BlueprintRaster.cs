// AIRGAP — blueprint rasterizer (Phase 3).
// Pure logic: converts a Blueprint into a cell grid (4 cells per tile) used by both the
// authoring validator (ValidatePhase3) and the scene loader (BlueprintScene).
// Works entirely in DATA space (x-right, y-DOWN, units = tiles) — no flipping here,
// no GameObjects, no editor APIs. Deterministic.
//
// Cell centers: p = (MinX + (cx + 0.5) / Scale, MinY + (cy + 0.5) / Scale).
// Carve order is significant and matches the numbered algorithm in the Phase 3 spec:
//   1 bounds  2 yard  3 building mass  4 space floors (+seams)  5 openings/doors
//   6 entrances  7 fence (+gates)  8 props  9 vents (duct/mouth/exterior housing)

using System.Collections.Generic;
using UnityEngine;

namespace AIRGAP.Facility.Blueprints
{
    public enum RasterCell : byte
    {
        Outside = 0,
        Wall = 1,
        Floor = 2,
        DoorFloor = 3,
        VentFloor = 4,
        YardFloor = 5,
    }

    public class BlueprintRaster
    {
        /// Cells per tile (cell = 0.25 tile).
        public const int Scale = 4;

        /// Data-space origin of cell (0,0).
        public float MinX, MinY;
        public int WidthCells, HeightCells;
        public RasterCell[,] Cells; // [cx, cy]

        /// Data coords of every vent endpoint (first and last point of each vent polyline).
        public List<Vector2> VentGrates;

        // BFS scratch — allocated once, reused across queries via a stamp counter.
        readonly int[,] _visited;
        int _visitStamp;
        readonly Queue<int> _queue = new Queue<int>();

        const float Eps = 1e-4f;

        public BlueprintRaster(Blueprint bp)
        {
            FenceInfo fence = bp.Grounds.Fence;

            // 1. Bounds — 1.5 tiles of margin around the fence. All cells start Outside.
            MinX = fence.X - 1.5f;
            MinY = fence.Y - 1.5f;
            float maxX = fence.X + fence.Width + 1.5f;
            float maxY = fence.Y + fence.Height + 1.5f;
            WidthCells = Mathf.CeilToInt((maxX - MinX) * Scale - Eps);
            HeightCells = Mathf.CeilToInt((maxY - MinY) * Scale - Eps);
            Cells = new RasterCell[WidthCells, HeightCells];
            _visited = new int[WidthCells, HeightCells];

            // 2. Yard: cells with center inside the fence rect.
            FillRect(fence.X, fence.Y, fence.X + fence.Width, fence.Y + fence.Height,
                RasterCell.YardFloor);

            // 3. Building mass: even-odd point-in-polygon over the footprint.
            List<float[]> footprint = bp.Building != null ? bp.Building.Footprint : null;
            if (footprint != null && footprint.Count >= 3)
            {
                for (int cx = 0; cx < WidthCells; cx++)
                for (int cy = 0; cy < HeightCells; cy++)
                {
                    Vector2 p = CellCenter(cx, cy);
                    if (PointInPolygonEvenOdd(p, footprint))
                        Cells[cx, cy] = RasterCell.Wall;
                }
            }

            // 4. Space floors: rects inset by 0.5 on all sides, plus seam carving between
            //    abutting rects of the same space.
            if (bp.Spaces != null)
            {
                foreach (BlueprintSpace space in bp.Spaces)
                {
                    if (space.Rects == null) continue;
                    foreach (float[] r in space.Rects)
                        FillRect(r[0] + 0.5f, r[1] + 0.5f, r[0] + r[2] - 0.5f, r[1] + r[3] - 0.5f,
                            RasterCell.Floor);

                    for (int i = 0; i < space.Rects.Count; i++)
                    for (int j = i + 1; j < space.Rects.Count; j++)
                    {
                        float[] a = space.Rects[i];
                        float[] b = space.Rects[j];
                        if (!RectsShareEdge(a, b)) continue;
                        // Intersection of (a expanded by 0.5) and (b expanded by 0.5),
                        // CLIPPED to the union of the two rects: the expansion reaches
                        // past the seam's open ends, and where an L-seam meets the
                        // building edge (R02/R15/R17) the unclipped fill would punch a
                        // walkable hole straight through the facade.
                        float x0 = Mathf.Max(a[0] - 0.5f, b[0] - 0.5f);
                        float y0 = Mathf.Max(a[1] - 0.5f, b[1] - 0.5f);
                        float x1 = Mathf.Min(a[0] + a[2] + 0.5f, b[0] + b[2] + 0.5f);
                        float y1 = Mathf.Min(a[1] + a[3] + 0.5f, b[1] + b[3] + 0.5f);
                        FillRectWithin(x0, y0, x1, y1, a, b, RasterCell.Floor);
                    }
                }
            }

            // 5. Openings carve Floor; doors carve DoorFloor.
            if (bp.Openings != null)
                foreach (BlueprintOpening o in bp.Openings)
                    CarveWallGap(o.X, o.Y, o.Length, o.Orientation, RasterCell.Floor);
            if (bp.Doors != null)
                foreach (BlueprintDoor d in bp.Doors)
                    CarveWallGap(d.X, d.Y, d.Length, d.Orientation, RasterCell.DoorFloor);

            // 6. Entrances kind "door" carve DoorFloor through the facade; hatches are
            //    markers only. The raster carves ALL entrances — sealing is the loader's
            //    job per assignment.
            if (bp.Entrances != null)
            {
                foreach (BlueprintEntrance e in bp.Entrances)
                {
                    if (e.Kind != "door") continue;
                    float half = e.Width * 0.5f;
                    if (e.Facing == "w" || e.Facing == "e")
                        FillRect(e.X - 0.5f, e.Y - half, e.X + 0.5f, e.Y + half,
                            RasterCell.DoorFloor);
                    else // "n" / "s"
                        FillRect(e.X - half, e.Y - 0.5f, e.X + half, e.Y + 0.5f,
                            RasterCell.DoorFloor);
                }
            }

            // 7. Fence: cells within 0.25 tiles of the perimeter lines -> Wall, except
            //    within a gate span (stays whatever it was — YardFloor inside).
            RasterizeFence(fence);

            // 8. Props: cells with center inside a prop rect -> Wall.
            if (bp.Grounds.Props != null)
                foreach (PropInfo prop in bp.Grounds.Props)
                    FillRect(prop.X, prop.Y, prop.X + prop.Width, prop.Y + prop.Height,
                        RasterCell.Wall);

            // 9. Vents: duct, mouths, exterior housing — in that order, per vent.
            VentGrates = new List<Vector2>();
            if (bp.Vents != null)
                foreach (BlueprintVent vent in bp.Vents)
                    RasterizeVent(vent);
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        public RasterCell CellAtData(float x, float y)
        {
            int cx = Mathf.FloorToInt((x - MinX) * Scale);
            int cy = Mathf.FloorToInt((y - MinY) * Scale);
            if (cx < 0 || cy < 0 || cx >= WidthCells || cy >= HeightCells)
                return RasterCell.Outside;
            return Cells[cx, cy];
        }

        public static bool IsWalkable(RasterCell c) =>
            c == RasterCell.Floor || c == RasterCell.DoorFloor ||
            c == RasterCell.VentFloor || c == RasterCell.YardFloor;

        /// BFS 4-neighbour over walkable cells. False if either endpoint's cell is
        /// unwalkable or out of bounds.
        public bool AreConnected(Vector2 aData, Vector2 bData)
        {
            int ax = Mathf.FloorToInt((aData.x - MinX) * Scale);
            int ay = Mathf.FloorToInt((aData.y - MinY) * Scale);
            int bx = Mathf.FloorToInt((bData.x - MinX) * Scale);
            int by = Mathf.FloorToInt((bData.y - MinY) * Scale);
            if (ax < 0 || ay < 0 || ax >= WidthCells || ay >= HeightCells) return false;
            if (bx < 0 || by < 0 || bx >= WidthCells || by >= HeightCells) return false;
            if (!IsWalkable(Cells[ax, ay]) || !IsWalkable(Cells[bx, by])) return false;
            if (ax == bx && ay == by) return true;

            _visitStamp++;
            _queue.Clear();
            _visited[ax, ay] = _visitStamp;
            _queue.Enqueue(ax + ay * WidthCells);
            while (_queue.Count > 0)
            {
                int packed = _queue.Dequeue();
                int cx = packed % WidthCells;
                int cy = packed / WidthCells;
                if (cx == bx && cy == by) return true;
                TryVisit(cx - 1, cy);
                TryVisit(cx + 1, cy);
                TryVisit(cx, cy - 1);
                TryVisit(cx, cy + 1);
            }
            return false;
        }

        void TryVisit(int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cx >= WidthCells || cy >= HeightCells) return;
            if (_visited[cx, cy] == _visitStamp) return;
            if (!IsWalkable(Cells[cx, cy])) return;
            _visited[cx, cy] = _visitStamp;
            _queue.Enqueue(cx + cy * WidthCells);
        }

        /// Samples every 0.2 tiles along the segment, inclusive of both ends; every sample
        /// must be Floor or DoorFloor (patrols stay indoors — never vents or the yard).
        public bool SegmentWalkableIndoors(Vector2 aData, Vector2 bData)
        {
            float length = Vector2.Distance(aData, bData);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.2f));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(aData, bData, i / (float)steps);
                RasterCell c = CellAtData(p.x, p.y);
                if (c != RasterCell.Floor && c != RasterCell.DoorFloor) return false;
            }
            return true;
        }

        /// Greedy merge of contiguous cells of one type: horizontal runs of height 1 cell,
        /// then vertical merging of runs with identical x-span. RectInt is in CELL
        /// coordinates. Keeps the loader's scene object count low.
        public List<RectInt> MergedWallRuns() => MergedRuns(RasterCell.Wall);

        public List<RectInt> MergedRuns(RasterCell match)
        {
            var result = new List<RectInt>();
            // (x, width) -> index into result of a rect whose bottom edge is the previous row.
            var active = new Dictionary<(int x, int w), int>();
            var next = new Dictionary<(int x, int w), int>();

            for (int cy = 0; cy < HeightCells; cy++)
            {
                next.Clear();
                int cx = 0;
                while (cx < WidthCells)
                {
                    if (Cells[cx, cy] != match) { cx++; continue; }
                    int start = cx;
                    while (cx < WidthCells && Cells[cx, cy] == match) cx++;
                    int width = cx - start;

                    var key = (start, width);
                    if (active.TryGetValue(key, out int idx) &&
                        result[idx].yMax == cy)
                    {
                        RectInt grown = result[idx];
                        grown.height += 1;
                        result[idx] = grown;
                        next[key] = idx;
                    }
                    else
                    {
                        result.Add(new RectInt(start, cy, width, 1));
                        next[key] = result.Count - 1;
                    }
                }
                // Swap active/next for the following row.
                var tmp = active; active = next; next = tmp;
            }
            return result;
        }

        /// Converts a cell-space rect back to a data-space rect (x, y, w, h in tiles).
        public Rect CellRectToDataRect(RectInt cells) => new Rect(
            MinX + cells.x / (float)Scale,
            MinY + cells.y / (float)Scale,
            cells.width / (float)Scale,
            cells.height / (float)Scale);

        // ------------------------------------------------------------------
        // Rasterization helpers
        // ------------------------------------------------------------------

        Vector2 CellCenter(int cx, int cy) =>
            new Vector2(MinX + (cx + 0.5f) / Scale, MinY + (cy + 0.5f) / Scale);

        /// Sets every cell whose CENTER lies inside [x0..x1] x [y0..y1] to `value`.
        /// <summary>FillRect restricted to cells whose center lies inside rect a or rect b.</summary>
        void FillRectWithin(float x0, float y0, float x1, float y1, float[] a, float[] b, RasterCell value)
        {
            if (x1 <= x0 || y1 <= y0) return;
            int cxMin = Mathf.Max(0, Mathf.FloorToInt((x0 - MinX) * Scale) - 1);
            int cxMax = Mathf.Min(WidthCells - 1, Mathf.CeilToInt((x1 - MinX) * Scale) + 1);
            int cyMin = Mathf.Max(0, Mathf.FloorToInt((y0 - MinY) * Scale) - 1);
            int cyMax = Mathf.Min(HeightCells - 1, Mathf.CeilToInt((y1 - MinY) * Scale) + 1);
            for (int cx = cxMin; cx <= cxMax; cx++)
            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                Vector2 p = CellCenter(cx, cy);
                if (p.x < x0 || p.x > x1 || p.y < y0 || p.y > y1) continue;
                bool insideA = p.x >= a[0] && p.x <= a[0] + a[2] && p.y >= a[1] && p.y <= a[1] + a[3];
                bool insideB = p.x >= b[0] && p.x <= b[0] + b[2] && p.y >= b[1] && p.y <= b[1] + b[3];
                if (insideA || insideB)
                    Cells[cx, cy] = value;
            }
        }

        void FillRect(float x0, float y0, float x1, float y1, RasterCell value)
        {
            if (x1 <= x0 || y1 <= y0) return;
            int cxMin = Mathf.Max(0, Mathf.FloorToInt((x0 - MinX) * Scale) - 1);
            int cxMax = Mathf.Min(WidthCells - 1, Mathf.CeilToInt((x1 - MinX) * Scale) + 1);
            int cyMin = Mathf.Max(0, Mathf.FloorToInt((y0 - MinY) * Scale) - 1);
            int cyMax = Mathf.Min(HeightCells - 1, Mathf.CeilToInt((y1 - MinY) * Scale) + 1);
            for (int cx = cxMin; cx <= cxMax; cx++)
            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                Vector2 p = CellCenter(cx, cy);
                if (p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1)
                    Cells[cx, cy] = value;
            }
        }

        /// Door/opening carve rect. Orientation "h": gap in a horizontal wall at y,
        /// spanning X..X+Length. "v": gap in a vertical wall at x, spanning Y..Y+Length.
        void CarveWallGap(float x, float y, float length, string orientation, RasterCell value)
        {
            if (orientation == "h")
                FillRect(x, y - 0.5f, x + length, y + 0.5f, value);
            else // "v"
                FillRect(x - 0.5f, y, x + 0.5f, y + length, value);
        }

        static bool RectsShareEdge(float[] a, float[] b)
        {
            float axMax = a[0] + a[2], ayMax = a[1] + a[3];
            float bxMax = b[0] + b[2], byMax = b[1] + b[3];
            float xOverlap = Mathf.Min(axMax, bxMax) - Mathf.Max(a[0], b[0]);
            float yOverlap = Mathf.Min(ayMax, byMax) - Mathf.Max(a[1], b[1]);
            bool abutX = Mathf.Abs(axMax - b[0]) <= 0.01f || Mathf.Abs(bxMax - a[0]) <= 0.01f;
            bool abutY = Mathf.Abs(ayMax - b[1]) <= 0.01f || Mathf.Abs(byMax - a[1]) <= 0.01f;
            return (abutX && yOverlap > 0.01f) || (abutY && xOverlap > 0.01f);
        }

        void RasterizeFence(FenceInfo fence)
        {
            float fx0 = fence.X, fy0 = fence.Y;
            float fx1 = fence.X + fence.Width, fy1 = fence.Y + fence.Height;
            // Perimeter segments: w (left), e (right), n (top, y-down), s (bottom).
            Vector2 nw = new Vector2(fx0, fy0), ne = new Vector2(fx1, fy0);
            Vector2 sw = new Vector2(fx0, fy1), se = new Vector2(fx1, fy1);
            List<GateInfo> gates = fence.Gates ?? new List<GateInfo>();

            int cxMin = Mathf.Max(0, Mathf.FloorToInt((fx0 - 0.3f - MinX) * Scale));
            int cxMax = Mathf.Min(WidthCells - 1, Mathf.CeilToInt((fx1 + 0.3f - MinX) * Scale));
            int cyMin = Mathf.Max(0, Mathf.FloorToInt((fy0 - 0.3f - MinY) * Scale));
            int cyMax = Mathf.Min(HeightCells - 1, Mathf.CeilToInt((fy1 + 0.3f - MinY) * Scale));

            for (int cx = cxMin; cx <= cxMax; cx++)
            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                Vector2 p = CellCenter(cx, cy);
                // Near a side (and not in that side's gate span) -> fence Wall.
                bool wall = false;
                if (DistToSegment(p, nw, sw) <= 0.25f && !InGate(gates, "w", p.y)) wall = true;
                else if (DistToSegment(p, ne, se) <= 0.25f && !InGate(gates, "e", p.y)) wall = true;
                else if (DistToSegment(p, nw, ne) <= 0.25f && !InGate(gates, "n", p.x)) wall = true;
                else if (DistToSegment(p, sw, se) <= 0.25f && !InGate(gates, "s", p.x)) wall = true;
                if (wall) Cells[cx, cy] = RasterCell.Wall;
            }
        }

        static bool InGate(List<GateInfo> gates, string side, float along)
        {
            for (int i = 0; i < gates.Count; i++)
            {
                GateInfo g = gates[i];
                if (g.Side == side && along >= g.From && along <= g.To) return true;
            }
            return false;
        }

        void RasterizeVent(BlueprintVent vent)
        {
            if (vent.Points == null || vent.Points.Count == 0) return;
            var pts = new List<Vector2>(vent.Points.Count);
            foreach (float[] p in vent.Points) pts.Add(new Vector2(p[0], p[1]));

            VentGrates.Add(pts[0]);
            VentGrates.Add(pts[pts.Count - 1]);

            // Bounding box of the polyline, expanded to cover all three bands.
            float bx0 = float.MaxValue, by0 = float.MaxValue;
            float bx1 = float.MinValue, by1 = float.MinValue;
            foreach (Vector2 p in pts)
            {
                bx0 = Mathf.Min(bx0, p.x); by0 = Mathf.Min(by0, p.y);
                bx1 = Mathf.Max(bx1, p.x); by1 = Mathf.Max(by1, p.y);
            }
            const float pad = 0.7f;
            int cxMin = Mathf.Max(0, Mathf.FloorToInt((bx0 - pad - MinX) * Scale));
            int cxMax = Mathf.Min(WidthCells - 1, Mathf.CeilToInt((bx1 + pad - MinX) * Scale));
            int cyMin = Mathf.Max(0, Mathf.FloorToInt((by0 - pad - MinY) * Scale));
            int cyMax = Mathf.Min(HeightCells - 1, Mathf.CeilToInt((by1 + pad - MinY) * Scale));

            Vector2 first = pts[0];
            Vector2 last = pts[pts.Count - 1];

            // (a) DUCT: within 0.25 of any segment AND currently Wall -> VentFloor.
            for (int cx = cxMin; cx <= cxMax; cx++)
            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                if (Cells[cx, cy] != RasterCell.Wall) continue;
                if (DistToPolyline(CellCenter(cx, cy), pts) <= 0.25f)
                    Cells[cx, cy] = RasterCell.VentFloor;
            }

            // (b) MOUTHS: within 0.45 of either endpoint AND currently Wall -> VentFloor
            //     (cells already Floor stay Floor — the mouth meets the room's own floor).
            for (int cx = cxMin; cx <= cxMax; cx++)
            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                if (Cells[cx, cy] != RasterCell.Wall) continue;
                Vector2 p = CellCenter(cx, cy);
                if (Vector2.Distance(p, first) <= 0.45f || Vector2.Distance(p, last) <= 0.45f)
                    Cells[cx, cy] = RasterCell.VentFloor;
            }

            // (c) EXTERIOR HOUSING: within 0.25..0.55 of any segment AND currently
            //     YardFloor -> Wall (seals ducts that run proud of the facade; never
            //     touches interior Floor).
            for (int cx = cxMin; cx <= cxMax; cx++)
            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                if (Cells[cx, cy] != RasterCell.YardFloor) continue;
                float d = DistToPolyline(CellCenter(cx, cy), pts);
                if (d >= 0.25f && d <= 0.55f)
                    Cells[cx, cy] = RasterCell.Wall;
            }
        }

        // ------------------------------------------------------------------
        // Geometry
        // ------------------------------------------------------------------

        /// Even-odd (ray casting) point-in-polygon test; polygon = [x,y] pairs.
        static bool PointInPolygonEvenOdd(Vector2 p, List<float[]> polygon)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = polygon[i][0], yi = polygon[i][1];
                float xj = polygon[j][0], yj = polygon[j][1];
                if ((yi > p.y) != (yj > p.y) &&
                    p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside;
        }

        static float DistToPolyline(Vector2 p, List<Vector2> pts)
        {
            if (pts.Count == 1) return Vector2.Distance(p, pts[0]);
            float best = float.MaxValue;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float d = DistToSegment(p, pts[i], pts[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        /// Standard closest-point-on-segment distance.
        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq <= 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
