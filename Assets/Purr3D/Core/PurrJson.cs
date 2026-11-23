// Parsing Purr API responses. Pure functions, no HTTP, so the tests can feed them captured JSON.
// Tolerant by design: junk elements are skipped rather than failing a whole room.

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Purr3D.Core
{
    /// <summary>The body wasn't the shape Purr promised.</summary>
    public sealed class PurrFormatException : Exception
    {
        public PurrFormatException(string message, Exception inner = null) : base(message, inner) { }
    }

    public static class PurrJson
    {
        /// <summary>"backlog" etc. to the enum. Unknown groups become Backlog, the safest place to park a task.</summary>
        public static StateGroup ParseStateGroup(string group)
        {
            switch (group)
            {
                case "unstarted": return StateGroup.Unstarted;
                case "started": return StateGroup.Started;
                case "completed": return StateGroup.Completed;
                case "cancelled": return StateGroup.Cancelled;
                default: return StateGroup.Backlog;
            }
        }

        /// <summary><c>GET /projects</c>: <c>{"items":[Project...]}</c>.</summary>
        public static List<PurrProject> ParseProjectList(string body)
        {
            var projects = new List<PurrProject>();
            foreach (var item in Items(body))
            {
                var project = ParseProject(item);
                if (project != null) projects.Add(project);
            }
            return projects;
        }

        /// <summary><c>GET /projects/{prefix}/tasks</c>: <c>{"items":[Task...], "cursor"?: string|null}</c>.</summary>
        public static TaskPage ParseTaskPage(string body)
        {
            var root = ReadObject(body);
            var page = new TaskPage { Cursor = String(root, "cursor") };
            foreach (var item in ItemsOf(root))
            {
                var task = ParseTask(item);
                if (task != null) page.Items.Add(task);
            }
            return page;
        }

        /// <summary>A single Task body, as <c>POST /tasks/{ref}/state</c> returns.</summary>
        public static PurrTask ParseTask(string body)
        {
            return ParseTask(ReadObject(body)) ?? throw new PurrFormatException("task has no ref or stateId");
        }

        /// <summary><c>{"error":{"code","message"}}</c>. False when the body isn't a Purr error.</summary>
        public static bool TryParseError(string body, out string code, out string message)
        {
            code = null;
            message = null;
            JObject root;
            try
            {
                root = ReadObject(body);
            }
            catch (PurrFormatException)
            {
                return false;
            }
            if (!(root["error"] is JObject error)) return false;
            code = String(error, "code");
            message = String(error, "message");
            return true;
        }

        private static PurrProject ParseProject(JObject json)
        {
            var id = String(json, "id");
            var prefix = String(json, "prefix");
            if (id == null || prefix == null) return null;

            var project = new PurrProject
            {
                Id = id,
                Prefix = prefix,
                Name = String(json, "name") ?? prefix,
                Description = String(json, "description") ?? "",
                Icon = String(json, "icon"),
                Archived = json.Value<bool?>("archived") ?? false,
            };
            if (json["states"] is JArray states)
            {
                foreach (var token in states)
                {
                    if (!(token is JObject s) || String(s, "id") == null) continue;
                    project.States.Add(new PurrState
                    {
                        Id = String(s, "id"),
                        Name = String(s, "name") ?? "",
                        Group = ParseStateGroup(String(s, "group")),
                        Order = Int(s, "order") ?? 0,
                    });
                }
            }
            // The API returns states in storage order, not Purr order. List.Sort isn't stable, so break ties by position.
            project.States = StableSortByOrder(project.States);
            return project;
        }

        private static PurrTask ParseTask(JObject json)
        {
            var reference = String(json, "ref");
            var stateId = String(json, "stateId");
            if (reference == null || stateId == null) return null;

            var task = new PurrTask
            {
                Id = String(json, "id"),
                Ref = reference,
                ProjectId = String(json, "projectId"),
                Title = String(json, "title") ?? "",
                StateId = stateId,
                Priority = String(json, "priority") ?? "none",
                Points = Int(json, "points"),
                Version = Int(json, "version") ?? 0,
                UpdatedAt = Date(json, "updatedAt"),
            };
            if (json["labels"] is JArray labels)
            {
                foreach (var label in labels)
                {
                    if (label.Type == JTokenType.String) task.Labels.Add((string)label);
                }
            }
            return task;
        }

        private static List<PurrState> StableSortByOrder(List<PurrState> states)
        {
            var indexed = new List<KeyValuePair<int, PurrState>>();
            for (var i = 0; i < states.Count; i++) indexed.Add(new KeyValuePair<int, PurrState>(i, states[i]));
            indexed.Sort((a, b) =>
            {
                var byOrder = a.Value.Order.CompareTo(b.Value.Order);
                return byOrder != 0 ? byOrder : a.Key.CompareTo(b.Key);
            });
            return indexed.ConvertAll(kv => kv.Value);
        }

        private static IEnumerable<JObject> Items(string body) => ItemsOf(ReadObject(body));

        private static IEnumerable<JObject> ItemsOf(JObject root)
        {
            if (!(root["items"] is JArray items)) throw new PurrFormatException("no \"items\" array");
            foreach (var token in items)
            {
                if (token is JObject obj) yield return obj;
            }
        }

        private static JObject ReadObject(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) throw new PurrFormatException("empty body");
            try
            {
                return JObject.Parse(body);
            }
            catch (JsonException e)
            {
                throw new PurrFormatException("body is not a JSON object", e);
            }
        }

        /// <summary>The field as a string, or null when absent, null or not a string.</summary>
        private static string String(JObject json, string field)
        {
            var token = json[field];
            return token != null && token.Type == JTokenType.String ? (string)token : null;
        }

        /// <summary>
        /// The field as a UTC instant, or null when absent or unparseable. JObject.Parse turns ISO strings into Date
        /// tokens by default, so both token types are accepted.
        /// </summary>
        private static DateTimeOffset? Date(JObject json, string field)
        {
            var token = json[field];
            if (token == null) return null;
            switch (token.Type)
            {
                case JTokenType.Date:
                    var value = ((JValue)token).Value;
                    if (value is DateTimeOffset offset) return offset.ToUniversalTime();
                    if (value is DateTime dateTime)
                    {
                        // Newtonsoft marks "...Z" strings as Utc. Anything Unspecified is treated as UTC too.
                        var utc = dateTime.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                            : dateTime.ToUniversalTime();
                        return new DateTimeOffset(utc);
                    }
                    return null;
                case JTokenType.String:
                    return DateTimeOffset.TryParse((string)token, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                        ? parsed
                        : (DateTimeOffset?)null;
                default:
                    return null;
            }
        }

        /// <summary>The field as an int, or null when absent, null or not a number.</summary>
        private static int? Int(JObject json, string field)
        {
            var token = json[field];
            if (token == null) return null;
            switch (token.Type)
            {
                case JTokenType.Integer: return (int)token;
                case JTokenType.Float: return (int)Math.Round((double)token);
                default: return null;
            }
        }
    }
}
