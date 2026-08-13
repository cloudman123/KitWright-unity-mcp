// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using KitWright.Editor.Tools.Helpers;

namespace KitWright.Editor.Tools.Builtins
{
    [ToolProvider("Reflection")]
    internal static class ReflectionFunctions
    {
        internal const BindingFlags DeclaredMembers =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        internal const BindingFlags InheritedMembers =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        private const int MaxCandidates = 10;
        private const int TypoPrefixLength = 4;

        private static Dictionary<string, List<Type>> s_byShortName;
        private static Dictionary<string, Type> s_byFullName;
        private static readonly object s_lock = new object();

        [Description("Inspect the live C# API by reflection — verify a type and its members exist before writing an execute_code snippet. " +
                     "Without 'member': declared public members of the type, names only (methods, properties, fields, events; enum values for enums). " +
                     "With 'member': full signatures including parameter names, falling back to inherited members. " +
                     "Unresolved names return candidate suggestions, so a typo costs one call instead of a failed compile.")]
        [ReadOnlyTool]
        public static object ReflectApi(
            [ToolParam("Type name, short ('Rigidbody', 'Mathf') or fully qualified ('UnityEngine.Rigidbody')")] string name,
            [ToolParam("Member name to get full signatures for. Omit to list member names.", Required = false)] string member = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Response.Error("EMPTY_NAME");

            var type = Resolve(name, out var matches, out var ambiguous);

            if (ambiguous)
                return Response.Error("AMBIGUOUS_TYPE", new
                {
                    query = name,
                    matches,
                    hint = "Several loaded types share this short name. Re-run with the fully qualified name."
                });

            if (type == null)
                return Response.Error("TYPE_NOT_FOUND", new { query = name, candidates = matches });

            // Reflecting over members of an open generic definition segfaults Mono, so stop at the type header.
            if (type.IsGenericTypeDefinition)
                return Response.Success($"Type header for '{type.Name}' (open generic).", Header(type, new
                {
                    is_generic_type_definition = true,
                    hint = "Open generic type — query a closed type or consult the docs for member details."
                }));

            return string.IsNullOrWhiteSpace(member)
                ? DescribeType(type)
                : DescribeMember(type, member.Trim());
        }

        private static object DescribeType(Type type)
        {
            if (type.IsEnum)
                return Response.Success($"Enum '{type.Name}' has {Enum.GetNames(type).Length} values.",
                    Header(type, new { values = Enum.GetNames(type) }));

            var methods = type.GetMethods(DeclaredMembers).Where(m => !m.IsSpecialName).Select(m => m.Name);
            var properties = type.GetProperties(DeclaredMembers).Select(p => p.Name);
            var fields = type.GetFields(DeclaredMembers).Select(f => f.Name);
            var events = type.GetEvents(DeclaredMembers).Select(e => e.Name);

            return Response.Success($"Declared public members of '{type.Name}'.", Header(type, new
            {
                members = new
                {
                    methods = Sorted(methods),
                    properties = Sorted(properties),
                    fields = Sorted(fields),
                    events = Sorted(events)
                },
                hint = "Names only — pass 'member' for full signatures. Inherited members are omitted here but still resolve when queried by name."
            }));
        }

        private static object DescribeMember(Type type, string member)
        {
            var signatures = Signatures(type, member, DeclaredMembers);
            var inherited = signatures.Length == 0;
            if (inherited)
                signatures = Signatures(type, member, InheritedMembers);

            if (signatures.Length == 0)
                return Response.Error("MEMBER_NOT_FOUND", new
                {
                    type = type.FullName,
                    query = member,
                    candidates = SimilarMembers(type, member)
                });

            return Response.Success($"{signatures.Length} signature(s) for '{type.Name}.{member}'.", new
            {
                type = type.FullName,
                member,
                inherited,
                signatures
            });
        }

        internal static string[] Signatures(Type type, string member, BindingFlags flags)
        {
            var results = new List<string>();

            foreach (var m in type.GetMethods(flags).Where(m => NameMatches(m.Name, member) && !m.IsSpecialName))
                results.Add(Obsolete(m) + Modifiers(m.IsStatic) + $"{Short(m.ReturnType)} {m.Name}({Parameters(m)})");

            foreach (var p in type.GetProperties(flags).Where(p => NameMatches(p.Name, member)))
                results.Add(Obsolete(p) + Modifiers(p.GetMethod?.IsStatic ?? false) +
                            $"{Short(p.PropertyType)} {p.Name} {{ {(p.CanRead ? "get; " : "")}{(p.CanWrite ? "set; " : "")}}}");

            foreach (var f in type.GetFields(flags).Where(f => NameMatches(f.Name, member)))
                results.Add(Obsolete(f) + Modifiers(f.IsStatic) + $"{Short(f.FieldType)} {f.Name}");

            foreach (var e in type.GetEvents(flags).Where(e => NameMatches(e.Name, member)))
                results.Add(Obsolete(e) + $"event {Short(e.EventHandlerType)} {e.Name}");

            return results.ToArray();
        }

        /// <summary>
        /// Resolve a type name across every loaded assembly. Returns null with <paramref name="ambiguous"/>
        /// set when a short name hits more than one type, so callers never silently pick the wrong one.
        /// </summary>
        internal static Type Resolve(string name, out string[] matches, out bool ambiguous)
        {
            matches = Array.Empty<string>();
            ambiguous = false;

            if (string.IsNullOrWhiteSpace(name))
                return null;

            var query = name.Trim();
            EnsureIndex();

            if (s_byFullName.TryGetValue(query, out var exact))
                return exact;

            var sameName = ExactCase(query) ?? IgnoringCase(query);
            if (sameName != null)
            {
                if (sameName.Count == 1)
                    return sameName[0];

                ambiguous = true;
                matches = sameName.Select(t => t.FullName)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .Take(MaxCandidates)
                    .ToArray();
                return null;
            }

            matches = SimilarTypes(query);
            return null;
        }

        private static List<Type> ExactCase(string query) =>
            s_byShortName.TryGetValue(query, out var hit) ? hit : null;

        // Type names are case-sensitive in C#; only fall back to a loose match when the exact
        // spelling misses, otherwise Mathf and System.MathF collide into a false ambiguity.
        private static List<Type> IgnoringCase(string query)
        {
            var loose = s_byShortName
                .Where(pair => string.Equals(pair.Key, query, StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value)
                .ToList();

            return loose.Count > 0 ? loose : null;
        }

        private static void EnsureIndex()
        {
            if (s_byShortName != null)
                return;

            lock (s_lock)
            {
                if (s_byShortName != null)
                    return;

                var byShortName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
                var byFullName = new Dictionary<string, Type>(StringComparer.Ordinal);

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in SafeGetTypes(assembly))
                    {
                        if (type == null || !type.IsPublic)
                            continue;

                        if (!byShortName.TryGetValue(type.Name, out var list))
                        {
                            list = new List<Type>();
                            byShortName[type.Name] = list;
                        }
                        list.Add(type);

                        if (type.FullName != null && !byFullName.ContainsKey(type.FullName))
                            byFullName[type.FullName] = type;
                    }
                }

                s_byFullName = byFullName;
                s_byShortName = byShortName;
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Partially loaded assembly: keep whatever did resolve.
                return ex.Types.Where(t => t != null);
            }
            catch (Exception)
            {
                return Array.Empty<Type>();
            }
        }

        private static string[] SimilarTypes(string query)
        {
            var prefix = query.Length <= TypoPrefixLength ? query : query.Substring(0, TypoPrefixLength);

            return s_byShortName.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                            k.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(k => Math.Abs(k.Length - query.Length))
                .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)
                .Take(MaxCandidates)
                .ToArray();
        }

        private static string[] SimilarMembers(Type type, string member)
        {
            var prefix = member.Length <= TypoPrefixLength ? member : member.Substring(0, TypoPrefixLength);

            return type.GetMembers(InheritedMembers)
                .Select(m => m.Name)
                .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                            n.IndexOf(member, StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Take(MaxCandidates)
                .ToArray();
        }

        private static object Header(Type type, object extra)
        {
            var header = new Dictionary<string, object>
            {
                ["name"] = type.Name,
                ["full_name"] = type.FullName,
                ["namespace"] = type.Namespace,
                ["assembly"] = type.Assembly.GetName().Name,
                ["base_class"] = type.BaseType?.FullName,
                ["is_static"] = type.IsAbstract && type.IsSealed,
                ["is_abstract"] = type.IsAbstract && !type.IsSealed,
                ["is_enum"] = type.IsEnum
            };

            foreach (var property in extra.GetType().GetProperties())
                header[property.Name] = property.GetValue(extra);

            return header;
        }

        private static bool NameMatches(string candidate, string member) =>
            string.Equals(candidate, member, StringComparison.OrdinalIgnoreCase);

        private static string Parameters(MethodInfo method) =>
            string.Join(", ", method.GetParameters().Select(p => $"{Short(p.ParameterType)} {p.Name}"));

        private static string Modifiers(bool isStatic) => isStatic ? "static " : string.Empty;

        private static string Obsolete(MemberInfo member) =>
            member.GetCustomAttribute<ObsoleteAttribute>() != null ? "[Obsolete] " : string.Empty;

        // Signatures get pasted straight into execute_code snippets, so emit C# keywords
        // rather than CLR type names ("float x", not "Single x").
        private static readonly Dictionary<string, string> CSharpKeywords = new Dictionary<string, string>
        {
            ["Void"] = "void",
            ["Boolean"] = "bool",
            ["Byte"] = "byte",
            ["SByte"] = "sbyte",
            ["Char"] = "char",
            ["Int16"] = "short",
            ["UInt16"] = "ushort",
            ["Int32"] = "int",
            ["UInt32"] = "uint",
            ["Int64"] = "long",
            ["UInt64"] = "ulong",
            ["Single"] = "float",
            ["Double"] = "double",
            ["Decimal"] = "decimal",
            ["String"] = "string",
            ["Object"] = "object"
        };

        private static string Short(Type type)
        {
            if (type == null)
                return "void";

            var name = type.Name;

            if (type.Namespace == "System" && CSharpKeywords.TryGetValue(name, out var keyword))
                return keyword;

            var tick = name.IndexOf('`');
            if (tick < 0)
                return name;

            var args = type.GetGenericArguments().Select(Short);
            return $"{name.Substring(0, tick)}<{string.Join(", ", args)}>";
        }

        private static string[] Sorted(IEnumerable<string> names) =>
            names.Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
