using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Kinsmen.Standards
{
    /// <summary>
    /// Kinsmen changes what a skill counts as in the moment and nothing else: a character's saved
    /// levels and progress are never written, skills the character never trained are never asked
    /// for (the game's GetSkillLevel and GetSkillFactor add an empty skill to the character, which
    /// the save then keeps), no method that raises, lowers, resets or loads skills is called, and
    /// nothing is patched but the hooks named here, none of them on a way skills rise or fall.
    /// The self-test is held to the same, but for running the game's Skill.Raise on skill objects
    /// of its own and asking GetSkillLevel of skills the character has trained.
    /// </summary>
    public class SkillSafetyStandardTests
    {
        /// <summary>The fields that hold a character's skills, as the save writes them.</summary>
        private static readonly HashSet<string> SavedFields = new HashSet<string>
        {
            "Skills.Skill.m_level", "Skills.Skill.m_accumulator", "Skills.m_skillData",
        };

        /// <summary>The methods that change a character's skills.</summary>
        private static readonly HashSet<string> Changing = new HashSet<string>
        {
            "Character.RaiseSkill", "Player.RaiseSkill", "Skills.RaiseSkill", "Skills.LowerAllSkills", "Skills.OnDeath",
            "Skills.CheatRaiseSkill", "Skills.CheatResetSkill", "Skills.ResetSkill", "Skills.Clear", "Skills.Load",
            "Skills.Skill.Raise",
        };

        /// <summary>The methods that add an empty skill to a character when asked of one not trained.</summary>
        private static readonly HashSet<string> Adding = new HashSet<string>
        {
            "Skills.GetSkillLevel", "Skills.GetSkillFactor", "Skills.GetRandomSkillFactor", "Skills.GetRandomSkillRange",
            "Character.GetSkillLevel", "Character.GetSkillFactor", "Player.GetSkillLevel", "Player.GetSkillFactor",
        };

        /// <summary>Every game method Kinsmen patches, each with what it is for.</summary>
        private static readonly Dictionary<string, string> Hooks = new Dictionary<string, string>
        {
            ["SEMan.ModifySkillLevel"] = "lowers the copy GetSkillLevel hands it, before effects add",
            ["SkillsDialog.Setup"] = "colours the held rows and adds the line saying why",
            ["SEMan.GetHUDStatusEffects"] = "shows the effect icon while a skill is held",
            ["ZNetScene.Awake"] = "registers the record's messages on joining a world",
        };

        /// <summary>The ways skills rise or fall, which no hook may sit on.</summary>
        private static readonly HashSet<string> GainAndLoss = new HashSet<string>
        {
            "Character.RaiseSkill", "Player.RaiseSkill", "Skills.RaiseSkill", "Skills.Skill.Raise", "SEMan.ModifyRaiseSkill",
            "Skills.OnDeath", "Skills.LowerAllSkills", "Player.OnDeath", "Skills.Load", "Skills.Save",
        };

        [Fact]
        public void NothingWritesASavedSkillLevelOrProgress()
        {
            var found = new List<string>();
            foreach (var (name, model) in KinsmenSource.All<IdentifierNameSyntax>())
            {
                if (!(model.GetSymbolInfo(name).Symbol is IFieldSymbol field) || !SavedFields.Contains(Name(field))) continue;
                if (Written(name)) found.Add($"{KinsmenSource.Where(name)} {Name(field)}");
            }
            Violations.None("write a character's saved skills", found);
        }

        [Fact]
        public void NothingReachesTheSavedSkillsByName()
        {
            var found = new List<string>();
            foreach (var (literal, _) in KinsmenSource.All<LiteralExpressionSyntax>())
            {
                if (!literal.IsKind(SyntaxKind.StringLiteralExpression)) continue;
                var text = literal.Token.ValueText;
                if (text == "m_level" || text == "m_accumulator" || text == "m_skillData") found.Add($"{KinsmenSource.Where(literal)} \"{text}\"");
            }
            Violations.None("name a saved skill field for reflection", found);
        }

        [Fact]
        public void NothingCallsAMethodThatChangesSkills()
        {
            var found = new List<string>();
            foreach (var (call, model) in KinsmenSource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var name = Name(method);
                if (!Changing.Contains(name)) continue;
                if (name == "Skills.Skill.Raise" && InSelfTest(call)) continue;
                found.Add($"{KinsmenSource.Where(call)} {name}");
            }
            Violations.None("call a method that changes a character's skills", found);
        }

        [Fact]
        public void KinsmenNeverAsksForASkillTheCharacterHasNotTrained()
        {
            var found = new List<string>();
            foreach (var (call, model) in KinsmenSource.In<InvocationExpressionSyntax>(KinsmenSource.Released))
            {
                if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method && Adding.Contains(Name(method)))
                    found.Add($"{KinsmenSource.Where(call)} {Name(method)}");
            }
            Violations.None("ask the game for a skill level in a way that adds the skill to the character", found);
        }

        [Fact]
        public void KinsmenPatchesOnlyItsNamedHooks()
        {
            var patched = Patched().ToList();
            var found = patched.Where(p => !Hooks.ContainsKey(p.Target)).Select(p => $"{p.Where} {p.Target}").ToList();
            found.AddRange(Hooks.Keys.Where(hook => patched.All(p => p.Target != hook)).Select(hook => "no patch on " + hook));
            Violations.None("differ from the hooks Kinsmen names", found);
        }

        [Fact]
        public void NoHookSitsOnAWaySkillsRiseOrFall()
        {
            Assert.Empty(Hooks.Keys.Where(GainAndLoss.Contains));
            var found = Patched().Where(p => GainAndLoss.Contains(p.Target)).Select(p => $"{p.Where} {p.Target}");
            Violations.None("patch a way skills rise or fall", found);
        }

        [Fact]
        public void HarmonyIsOnlyToldOfPatchesByAttribute()
        {
            // A patch made by a call (Harmony.Patch) names its target in code the attribute check cannot read.
            var found = new List<string>();
            foreach (var (call, model) in KinsmenSource.In<InvocationExpressionSyntax>(KinsmenSource.Released))
            {
                if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method && method.ContainingType?.ToDisplayString() == "HarmonyLib.Harmony" && method.Name == "Patch")
                    found.Add(KinsmenSource.Where(call));
            }
            Violations.None("patch by a call instead of an attribute", found);
        }

        /// <summary>Each [HarmonyPatch(typeof(X), "Method")] in Kinsmen, as "X.Method".</summary>
        private static IEnumerable<(string Target, string Where)> Patched()
        {
            foreach (var (attribute, model) in KinsmenSource.In<AttributeSyntax>(KinsmenSource.Released))
            {
                if (model.GetTypeInfo(attribute).Type?.ToDisplayString() != "HarmonyLib.HarmonyPatch") continue;
                var args = attribute.ArgumentList?.Arguments;
                if (args == null || args.Value.Count < 2) continue;
                if (!(args.Value[0].Expression is TypeOfExpressionSyntax type)) continue;
                var target = model.GetTypeInfo(type.Type).Type?.ToDisplayString();
                var method = model.GetConstantValue(args.Value[1].Expression).Value as string;
                yield return ($"{target}.{method}", KinsmenSource.Where(attribute));
            }
        }

        private static string Name(ISymbol symbol) => symbol.ContainingType.ToDisplayString() + "." + symbol.Name;

        private static bool InSelfTest(SyntaxNode node) => KinsmenSource.Relative(node.SyntaxTree).StartsWith("selftest/", System.StringComparison.Ordinal);

        /// <summary>Whether a field's use writes it: assigned, changed in place, counted up or down, or handed on by reference.</summary>
        private static bool Written(ExpressionSyntax name)
        {
            ExpressionSyntax use = name;
            while (use.Parent is MemberAccessExpressionSyntax access && access.Name == use) use = access;
            while (use.Parent is ElementAccessExpressionSyntax element && element.Expression == use) use = element;
            switch (use.Parent)
            {
                case AssignmentExpressionSyntax assignment: return assignment.Left == use;
                case PrefixUnaryExpressionSyntax prefix: return prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression);
                case PostfixUnaryExpressionSyntax _: return true;
                case ArgumentSyntax argument: return !argument.RefKindKeyword.IsKind(SyntaxKind.None);
                case RefExpressionSyntax _: return true;
            }
            return false;
        }
    }
}
