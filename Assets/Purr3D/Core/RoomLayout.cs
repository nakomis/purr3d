// Where everything goes in a project's room: the floor is a board, one zone per state, left to right in
// Purr's order, and each task is a crate in its state's zone.
//
// Room space: the entrance is at z = 0, you look along +Z, and the room is centred on x = 0. Positions are
// in metres; a slot's Layer is how many crates sit beneath it (the builder turns that into a height).

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Purr3D.Core
{
    public sealed class RoomLayoutSettings
    {
        /// <summary>Width of each state's zone.</summary>
        public float ZoneWidth = 4f;

        /// <summary>Crates across a zone.</summary>
        public int Columns = 5;

        /// <summary>Centre-to-centre spacing between crates, across and deep.</summary>
        public float Pitch = 0.7f;

        /// <summary>Rows before a zone starts stacking instead of growing deeper.</summary>
        public int MaxRows = 12;

        /// <summary>Clear floor between the entrance and the first row, for arriving.</summary>
        public float FrontMargin = 3f;

        /// <summary>Floor behind the deepest row.</summary>
        public float BackMargin = 1.5f;

        /// <summary>The shallowest a room gets, so an empty project still has a room.</summary>
        public float MinDepth = 8f;
    }

    public sealed class RoomZone
    {
        /// <summary>The state, or null for the zone holding tasks whose state the project doesn't list.</summary>
        public PurrState State;

        public string Name;
        public int Index;
        public float CentreX;
        public float Width;

        /// <summary>Tasks in this zone.</summary>
        public int Count;

        /// <summary>Rows in use on the bottom layer.</summary>
        public int Rows;

        /// <summary>Layers in use: 1 for a zone that hasn't stacked, 0 when empty.</summary>
        public int Layers;

        public override string ToString() => $"{Name}: {Count}";
    }

    public sealed class TaskSlot
    {
        public string Ref;
        public int ZoneIndex;
        public int Column;
        public int Row;
        public int Layer;
        public float X;
        public float Z;

        public bool SamePlaceAs(TaskSlot other) =>
            other != null && other.ZoneIndex == ZoneIndex && other.Column == Column && other.Row == Row && other.Layer == Layer;

        public override string ToString() => $"{Ref} @ zone {ZoneIndex} ({Column}, {Row}, {Layer})";
    }

    public sealed class RoomPlan
    {
        public List<RoomZone> Zones = new List<RoomZone>();

        /// <summary>Every task's slot, by ref.</summary>
        public Dictionary<string, TaskSlot> Slots = new Dictionary<string, TaskSlot>(StringComparer.Ordinal);

        /// <summary>Total width (all zones) and depth (entrance to back wall).</summary>
        public float Width;
        public float Depth;
    }

    public static class RoomLayout
    {
        public const string UnknownZoneName = "Unknown";

        public static RoomPlan Build(PurrProject project, IEnumerable<PurrTask> tasks, RoomLayoutSettings settings = null)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            var s = settings ?? new RoomLayoutSettings();
            var columns = Math.Max(1, s.Columns);
            var maxRows = Math.Max(1, s.MaxRows);
            var perLayer = columns * maxRows;

            // Zones follow Purr's order; the list position breaks ties so the result is stable.
            var states = new List<KeyValuePair<int, PurrState>>();
            for (var i = 0; i < project.States.Count; i++)
            {
                if (project.States[i] != null) states.Add(new KeyValuePair<int, PurrState>(i, project.States[i]));
            }
            states.Sort((a, b) =>
            {
                var byOrder = a.Value.Order.CompareTo(b.Value.Order);
                return byOrder != 0 ? byOrder : a.Key.CompareTo(b.Key);
            });

            var buckets = new List<List<PurrTask>>();
            var zoneOf = new Dictionary<string, int>(StringComparer.Ordinal);
            var plan = new RoomPlan();
            foreach (var pair in states)
            {
                if (pair.Value.Id != null && !zoneOf.ContainsKey(pair.Value.Id)) zoneOf[pair.Value.Id] = plan.Zones.Count;
                plan.Zones.Add(new RoomZone { State = pair.Value, Name = pair.Value.Name ?? pair.Value.Id, Index = plan.Zones.Count });
                buckets.Add(new List<PurrTask>());
            }

            List<PurrTask> unknown = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    if (task?.Ref == null || !seen.Add(task.Ref)) continue;
                    if (task.StateId != null && zoneOf.TryGetValue(task.StateId, out var zone)) buckets[zone].Add(task);
                    else (unknown ?? (unknown = new List<PurrTask>())).Add(task);
                }
            }
            if (unknown != null)
            {
                plan.Zones.Add(new RoomZone { Name = UnknownZoneName, Index = plan.Zones.Count });
                buckets.Add(unknown);
            }

            plan.Width = plan.Zones.Count * s.ZoneWidth;
            var deepestRows = 0;
            for (var z = 0; z < plan.Zones.Count; z++)
            {
                var zone = plan.Zones[z];
                zone.Width = s.ZoneWidth;
                zone.CentreX = -plan.Width / 2f + s.ZoneWidth * (z + 0.5f);

                var bucket = buckets[z];
                bucket.Sort(NewestFirst);
                zone.Count = bucket.Count;
                zone.Rows = Math.Min(maxRows, (bucket.Count + columns - 1) / columns);
                zone.Layers = (bucket.Count + perLayer - 1) / perLayer;
                deepestRows = Math.Max(deepestRows, zone.Rows);

                for (var k = 0; k < bucket.Count; k++)
                {
                    var within = k % perLayer;
                    var column = within % columns;
                    var row = within / columns;
                    plan.Slots[bucket[k].Ref] = new TaskSlot
                    {
                        Ref = bucket[k].Ref,
                        ZoneIndex = z,
                        Column = column,
                        Row = row,
                        Layer = k / perLayer,
                        X = zone.CentreX + (column - (columns - 1) / 2f) * s.Pitch,
                        Z = s.FrontMargin + s.Pitch / 2f + row * s.Pitch,
                    };
                }
            }

            plan.Depth = Math.Max(s.MinDepth, s.FrontMargin + deepestRows * s.Pitch + s.BackMargin);
            return plan;
        }

        /// <summary>Most recently updated first (undated last), then by ref number, then by ref.</summary>
        private static int NewestFirst(PurrTask a, PurrTask b)
        {
            if (a.UpdatedAt.HasValue != b.UpdatedAt.HasValue) return a.UpdatedAt.HasValue ? -1 : 1;
            if (a.UpdatedAt.HasValue)
            {
                var byTime = b.UpdatedAt.Value.CompareTo(a.UpdatedAt.Value);
                if (byTime != 0) return byTime;
            }
            var byNumber = RefNumber(a.Ref).CompareTo(RefNumber(b.Ref));
            return byNumber != 0 ? byNumber : string.CompareOrdinal(a.Ref, b.Ref);
        }

        /// <summary>42 for "HOME-42"; int.MaxValue when there's no number, so those sort last.</summary>
        public static int RefNumber(string reference)
        {
            if (reference == null) return int.MaxValue;
            var dash = reference.LastIndexOf('-');
            return dash >= 0 && int.TryParse(reference.Substring(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                ? n
                : int.MaxValue;
        }
    }
}
