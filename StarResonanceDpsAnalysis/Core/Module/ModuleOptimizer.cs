using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Core.Module
{
    public class ModuleOptimizer
    {
        // —— 超标容差：允许比“期望等级”最多高出这么多“级”而不扣分（默认 1 级）
        private const int OvershootToleranceLevels = 1;
        // —— 超过容差后，每超出 1 级的惩罚（保持你原本的力度 200）
        private const double OvershootHardPenaltyPerLevel = 50;


        // 目标等级映射：属性名 -> 期望等级（1~6，0 或缺省表示不限制）
        private readonly Dictionary<string, int> _desiredLevels = new(StringComparer.Ordinal);

        public void SetDesiredLevels(Dictionary<string, int> desiredLevels)
        {
            _desiredLevels.Clear();
            if (desiredLevels == null) return;
            foreach (var kv in desiredLevels)
            {
                // 只保留 >0 的目标
                if (kv.Value > 0) _desiredLevels[kv.Key] = kv.Value;
            }
        }
        // 等级接近度：差距越小越好；差 0 得 6 分，差 1 得 5 分 ... 差>=6 得 0
        private double ComputeCloseness(Dictionary<string, int> breakdown)
        {
            if (_desiredLevels.Count == 0) return 0.0;

            double closeness = 0.0;
            const int MaxPerAttr = 6; // 单项满分

            foreach (var kv in _desiredLevels)
            {
                var name = kv.Key;
                var desired = kv.Value;
                if (desired <= 0) continue;

                if (!breakdown.TryGetValue(name, out var v)) continue;

                int level = ToLevel(v); // 你已有的等级换算

                if (level >= desired)
                {
                    // 达到或超过期望等级：给满分，不再因超过而扣分
                    closeness += MaxPerAttr;
                }
                else
                {
                    // 未达标：按差距扣分（差1→5分，差2→4分，最低0）
                    int diff = desired - level;
                    int score = MaxPerAttr - diff;
                    if (score < 0) score = 0;
                    closeness += score;
                }
            }

            return closeness;
        }



        // ModuleOptimizer.cs 顶部字段区
        private readonly HashSet<string> _priorityAttrs;
        // ====== 配置/依赖（通过构造函数注入） ======
        public enum ModuleCategory { ATTACK, GUARD, SUPPORT, ALL }

        public interface IModulePart
        {
            string Name { get; }
            int Id { get; }
            int Value { get; }
        }
        /// <summary>
        /// 排名方式
        /// </summary>
        public enum SortMode
        {
            /// <summary>
            /// 按战力排序
            /// </summary>
            ByScore,
            /// <summary>
            /// 按属性分解排序
            /// </summary>
            ByTotalAttr    // 总属性
        }

        public interface IModuleInfo
        {
            string Uuid { get; }      // 唯一标识，用于去重
            string Name { get; }
            int Quality { get; }
            int ConfigId { get; }
            IReadOnlyList<IModulePart> Parts { get; }
        }

        private readonly IReadOnlyDictionary<int, ModuleCategory> _moduleCategoryMap;
        private readonly IReadOnlyList<int> _attrThresholds;
        private readonly IReadOnlyDictionary<int, int> _basicAttrPowerMap;
        private readonly IReadOnlyDictionary<int, int> _specialAttrPowerMap;
        private readonly IReadOnlyDictionary<int, int> _totalAttrPowerMap;
        private readonly ISet<int> _basicAttrIds;
        private readonly ISet<int> _specialAttrIds;
        private readonly IReadOnlyDictionary<string, string> _attrNameTypeMap; // "basic"/"special"

        // ====== 参数/状态 ======
        private readonly Random _rand = new();
        private string _resultLogFile = null;

        public int LocalSearchIterations { get; set; } = 30;
        public int MaxSolutions { get; set; } = 60;


        // 属性等级权重
        private readonly Dictionary<int, double> _levelWeights = new()
    {
        {1, 1.0},
        {2, 4.0},
        {3, 8.0},
        {4, 12.0},
        {5, 16.0},
        {6, 20.0},
    };

        public ModuleOptimizer(
            IReadOnlyDictionary<int, ModuleCategory> moduleCategoryMap,
            IReadOnlyList<int> attrThresholds,
            IReadOnlyDictionary<int, int> basicAttrPowerMap,
            IReadOnlyDictionary<int, int> specialAttrPowerMap,
            IReadOnlyDictionary<int, int> totalAttrPowerMap,
            ISet<int> basicAttrIds,
            ISet<int> specialAttrIds,
            IReadOnlyDictionary<string, string> attrNameTypeMap,
            IEnumerable<string>? priorityAttrNames = null,
            string resultLogFile = null
        )
        {
            _moduleCategoryMap = moduleCategoryMap;
            _attrThresholds = attrThresholds;
            _basicAttrPowerMap = basicAttrPowerMap;
            _specialAttrPowerMap = specialAttrPowerMap;
            _totalAttrPowerMap = totalAttrPowerMap;
            _basicAttrIds = basicAttrIds;
            _specialAttrIds = specialAttrIds;
            _attrNameTypeMap = attrNameTypeMap;
            _resultLogFile = resultLogFile;
            _priorityAttrs = priorityAttrNames != null
                ? new HashSet<string>(priorityAttrNames, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }

        // ====== 解结构 ======
        public sealed class ModuleSolution
        {
            public List<IModuleInfo> Modules { get; }
            public double Score { get; }
            public Dictionary<string, int> AttrBreakdown { get; }
            public int PriorityLevel { get; }            // 新增：勾选属性最高等级（用于排序/比较）

            // 新增：总属性值
            public int TotalAttrValue { get; }
            public ModuleSolution(List<IModuleInfo> modules, double score,
                  Dictionary<string, int> attrBreakdown, int priorityLevel = 0)
            {
                Modules = modules;
                Score = score;
                AttrBreakdown = attrBreakdown;
                PriorityLevel = priorityLevel;
                TotalAttrValue = attrBreakdown?.Values.Sum() ?? 0;   // ← 新增
            }
        }

        // ====== 工具 ======
        private void LogResult(string message)
        {
            if (string.IsNullOrEmpty(_resultLogFile)) return;
            try
            {
                File.AppendAllText(_resultLogFile, message + Environment.NewLine);
            }
            catch { /* 忽略日志异常 */ }
        }

        private ModuleCategory GetModuleCategory(IModuleInfo module)
            => _moduleCategoryMap.TryGetValue(module.ConfigId, out var cat) ? cat : ModuleCategory.ATTACK;

        // attr_name -> "basic"/"special"
        private string GetAttrTypeByName(string attrName, IEnumerable<IModuleInfo> modules)
        {
            if (_attrNameTypeMap.TryGetValue(attrName, out var t)) return t;

            foreach (var m in modules)
            {
                foreach (var p in m.Parts)
                {
                    if (p.Name == attrName)
                    {
                        if (_basicAttrIds.Contains(p.Id)) return "basic";
                        if (_specialAttrIds.Contains(p.Id)) return "special";
                        return "basic";
                    }
                }
            }
            return "basic";
        }

        private int ToLevel(int value)
        {
            int level = 0;
            for (int i = 0; i < _attrThresholds.Count; i++)
            {
                if (value >= _attrThresholds[i]) level = i + 1;
                else break;
            }
            return level;
        }

        private (int priorityLevel, int combatPower, Dictionary<string, int> breakdown) Evaluate(IReadOnlyList<IModuleInfo> modules)
        {
            var breakdown = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var m in modules)
                foreach (var p in m.Parts)
                    breakdown[p.Name] = breakdown.TryGetValue(p.Name, out var cur) ? cur + p.Value : p.Value;

            // 勾选属性最高等级（无勾选则为 0）
            int priorityLevel = 0;
            if (_priorityAttrs != null && _priorityAttrs.Count > 0)
            {
                foreach (var name in _priorityAttrs)
                    if (breakdown.TryGetValue(name, out var v))
                        priorityLevel = Math.Max(priorityLevel, ToLevel(v));
            }

            var (combatPower, _) = CalculateCombatPower(modules);  // 你原来的战斗力
            return (priorityLevel, combatPower, breakdown);
        }

        // ====== 评分函数：统一将 “白名单覆盖” 放最高优先 ======
        // 评分：目标白名单(有期望) >> 白名单(无期望) >> 非白名单 >> 战力
        // 其中：对“有期望”的属性，超标会被强扣分（绝不优于刚好达标）
        private (double score, Dictionary<string, int> breakdown, int priorityMaxLevel)
            CalculatePriorityAwareScore(IReadOnlyList<IModuleInfo> modules)
        {
            // 1) 基础统计
            var (combatPower, breakdown) = CalculateCombatPower(modules);

            // 2) 计算各属性的“效果等级”1..6
            int priorityMaxLevel = 0;
            var levelByAttr = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in breakdown)
            {
                string attr = kv.Key;
                int lvl = 0;
                for (int i = 0; i < _attrThresholds.Count; i++)
                {
                    if (kv.Value >= _attrThresholds[i]) lvl = i + 1;
                    else break;
                }
                levelByAttr[attr] = lvl;
                if (lvl > priorityMaxLevel) priorityMaxLevel = lvl;
            }

            var whitelist = _priorityAttrs ?? new HashSet<string>(StringComparer.Ordinal);
            var desired = _desiredLevels ?? new Dictionary<string, int>(StringComparer.Ordinal);

            // 3) 三层分数桶
            double tier1 = 0.0; // 白名单且有期望（最优先）
            double tier2 = 0.0; // 白名单无期望
            double tier3 = 0.0; // 非白名单

            int globalCloseness = (int)ComputeCloseness(breakdown); // 仅做同分细分

            foreach (var (attr, lvl) in levelByAttr)
            {
                _levelWeights.TryGetValue(lvl, out var w); // 1→1, 2→4, …, 6→20

                bool isWhite = whitelist.Contains(attr);
                bool hasTarget = desired.TryGetValue(attr, out var targetLvl);

                if (isWhite && hasTarget)
                {
                    // —— 新逻辑：无论期望如何，整体往 6 凑；允许小幅超出期望不扣分
                    const int AimLevel = 6;

                    if (lvl >= AimLevel)
                    {
                        // 命中 6 级：直接给最强奖励（视为理想命中）
                        tier1 += 2000.0;
                    }
                    else if (lvl >= targetLvl)
                    {
                        // 达到或超过期望但尚未到 6
                        int overshoot = lvl - targetLvl; // >= 0

                        if (overshoot <= OvershootToleranceLevels)
                        {
                            // 小幅超出期望：不扣分（视作“合理溢出”）
                            tier1 += 2000.0;
                        }
                        else
                        {
                            // 超过“容差”之后才开始扣
                            int hard = overshoot - OvershootToleranceLevels;
                            tier1 += 2000.0 - hard * OvershootHardPenaltyPerLevel;
                        }
                    }
                    else
                    {
                        // 未达标：仍然按“接近程度”给分（与你原来的权重一致）
                        int diff = Math.Min(6, targetLvl - lvl);
                        int closenessLocal = 6 - diff;      // 0..6
                        tier1 += closenessLocal * 20.0;     // 适中权重（保持原有手感）
                    }

                    // 注意：有期望的属性，不再额外叠加 w 避免“超标因为权重更高反而被奖励”
                }
                else if (isWhite)
                {
                    // 白名单但无期望：谁等级更高谁好（直接用等级权重）
                    tier2 += w;
                }
                else
                {
                    // 非白名单：最低优先
                    tier3 += w;
                }
            }

            // 4) 组合分数：Tier1 >> Tier2 >> Tier3 >> 贴近度/战力 做细分
            double score =
                  tier1 * 1_000_000_000.0
                + tier2 * 1_000_000.0
                + tier3 * 10_000.0
                + globalCloseness * 1_000.0
                + combatPower;

            return (score, breakdown, priorityMaxLevel);
        }




        // ====== 预筛选（按每种属性取前30条） ======
        public List<IModuleInfo> PrefilterModules(IReadOnlyList<IModuleInfo> modules)
        {
            var attrModules = new Dictionary<string, List<(IModuleInfo m, int v)>>();
            foreach (var module in modules)
            {
                foreach (var part in module.Parts)
                {
                    if (!attrModules.TryGetValue(part.Name, out var list))
                    {
                        list = new List<(IModuleInfo, int)>();
                        attrModules[part.Name] = list; // creates list of all attributes you own
                    }
                    list.Add((module, part.Value));
                }
            }

            var candidate = new List<IModuleInfo>();
            int count = 0;
            foreach (var kv in attrModules)
            {
                var top = kv.Value
                    .OrderByDescending(x => x.v) // v = level of iteration's attribute ie; first entry special atk's level is 8
                    .Take(30) // takes 30 top of specific attributes based on order of their level ie; special atk 10 -> special atk 7 is last element. increase to allow more modules?
                    .Select(x => x.m); //bug, doesnt seem to be adding all 50?

                count += top.Count();

                foreach (var m in top) {
                    candidate.Add(m);
                    /*
                    if (!candidate.Add(m))
                    {
                        foreach(var p in m.Parts) // same parts but the quality is different...this is a problem.
                        {
                            Debug.WriteLine(p.Id + ":" + p.Name + ":" + p.Value);
                        }
                        Debug.WriteLine("========");
                    }
                    */
                    // add list of selected attributes filtered to be only 50 of the top most, add to candidate. duplicates arent added
                }
            }
            return candidate;
        }

        // ====== 战斗力计算（阈值战力 + 总属性战力） ======
        public (int power, Dictionary<string, int> attrBreakdown) CalculateCombatPower(IReadOnlyList<IModuleInfo> modules)
        {
            var breakdown = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var m in modules)
            {
                foreach (var p in m.Parts)
                {
                    breakdown[p.Name] = breakdown.TryGetValue(p.Name, out var cur) ? cur + p.Value : p.Value;
                }
            }

            int thresholdPower = 0;

            foreach (var (attrName, attrValue) in breakdown)
            {
                int maxLevel = 0;
                for (int i = 0; i < _attrThresholds.Count; i++)
                {
                    if (attrValue >= _attrThresholds[i]) maxLevel = i + 1;
                    else break;
                }

                var attrType = ModuleMaps.ATTR_NAME_TYPE_MAP.TryGetValue(attrName, out var t) ? t : "basic"; //this funky shit didnt take into account translated text. who the fk hardcodes this shit with strings?
                var map = (attrType == "special") ? _specialAttrPowerMap : _basicAttrPowerMap;
                if (maxLevel > 0 && map.TryGetValue(maxLevel, out var add))
                    thresholdPower += add;
            }

            int totalAttrValue = breakdown.Values.Sum();
            int totalAttrPower = _totalAttrPowerMap.TryGetValue(totalAttrValue, out var tap) ? tap : 0;
            int totalPower = thresholdPower + totalAttrPower;
            return (totalPower, breakdown);
        }

        // ====== 评分函数（如果你项目里只用战斗力，可不调用这个） ======
        public (double score, Dictionary<string, int> attrBreakdown) CalculateSolutionScore(IReadOnlyList<IModuleInfo> modules)
        {
            var breakdown = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var m in modules)
                foreach (var p in m.Parts)
                    breakdown[p.Name] = breakdown.TryGetValue(p.Name, out var cur) ? cur + p.Value : p.Value;

            double highLevelScore = 0.0;
            foreach (var (_, attrValue) in breakdown)
            {
                int level = 0;
                for (int i = 0; i < _attrThresholds.Count; i++)
                {
                    if (attrValue >= _attrThresholds[i]) level = i + 1;
                    else break;
                }
                if (level > 0 && _levelWeights.TryGetValue(level, out var w)) highLevelScore += w;
            }

            int totalValue = breakdown.Values.Sum();

            double totalWaste = 0.0;
            foreach (var (_, value) in breakdown)
            {
                int maxThreshold = 0;
                foreach (var th in _attrThresholds)
                {
                    if (value >= th) maxThreshold = th;
                    else break;
                }
                totalWaste += (value - maxThreshold);
            }

            double score = 0.9 * highLevelScore + 0.05 * totalValue - 0.05 * totalWaste;
            return (score, breakdown);
        }

        // ====== 贪心构造初始解（4件） ======
        public ModuleSolution GreedyConstructSolution(IReadOnlyList<IModuleInfo> modules)
        {
            if (modules.Count < 1) return null;

            var current = new List<IModuleInfo> { modules[_rand.Next(modules.Count)] }; //randomly gets a combination of modules to create a module solution from.

            for (int k = 0; k < 3; k++) // can only make a solution with 4 modules
            {
                IModuleInfo pick = null;
                double bestScore = double.NegativeInfinity;

                foreach (var m in modules) // checks all candidate modules that give best score, calcuating priority aware score
                {
                    if (current.Contains(m)) continue;

                    var test = current.Concat(new[] { m }).ToList();
                    var (sc, _, _) = CalculatePriorityAwareScore(test); // ← 统一评分

                    if (sc > bestScore)
                    {
                        bestScore = sc;
                        pick = m;
                    }
                }

                if (pick == null) break;
                current.Add(pick);
            }

            // 为 UI/结构补齐 PriorityLevel 和 breakdown
            var (pri, pow, bd) = Evaluate(current); // 你已有的接口:contentReference[oaicite:5]{index=5}
            return new ModuleSolution(current, pow, bd, pri);
        }

        // ====== 局部搜索（单点替换提升） ======
        public ModuleSolution LocalSearchImprove(ModuleSolution solution, IReadOnlyList<IModuleInfo> allModules)
        {
            if (solution == null) return null;
            var best = new ModuleSolution(
                new List<IModuleInfo>(solution.Modules),
                solution.Score,
                new Dictionary<string, int>(solution.AttrBreakdown),
                solution.PriorityLevel
            );

            // 现状最好分数（用统一评分）
            var (bestScoreUnified, _, _) = CalculatePriorityAwareScore(best.Modules);

            for (int iter = 0; iter < LocalSearchIterations; iter++)
            {
                bool improved = false;

                for (int i = 0; i < best.Modules.Count; i++)
                {
                    int take = Math.Min(20, allModules.Count);
                    var sample = allModules.OrderBy(_ => _rand.Next()).Take(take); //its random lol

                    foreach (var nm in sample)
                    {
                        if (best.Modules.Contains(nm)) continue;

                        var newModules = new List<IModuleInfo>(best.Modules);
                        newModules[i] = nm;

                        var (sc, bd, _) = CalculatePriorityAwareScore(newModules);
                        if (sc > bestScoreUnified)
                        {
                            var (pri, pow, _) = Evaluate(newModules);
                            best = new ModuleSolution(newModules, pow, bd, pri);
                            bestScoreUnified = sc;
                            improved = true;
                            break;
                        }
                    }
                    if (improved) break;
                }
                if (!improved && iter > LocalSearchIterations / 2) break;
            }
            return best;
        }



        // ====== 主流程：优化 ======
        public List<ModuleSolution> OptimizeModules(
            IReadOnlyList<IModuleInfo> modules,
            ModuleCategory category,
            int topN = 40,
                SortMode sortMode = SortMode.ByTotalAttr   // ← 新增参数，默认按属性

          )
        {
            // 过滤类型
            List<IModuleInfo> filtered = (category == ModuleCategory.ALL)
                ? modules.ToList()
                : modules.Where(m => GetModuleCategory(m) == category).ToList();

            if (filtered.Count < 1) return new List<ModuleSolution>();

            // 预筛选

            var candidates = PrefilterModules(filtered); // pulls top 30 of sorted list of modules by their combined attribute level
            // test with ALL attributes no 30 limit - no change

            var solutions = new List<ModuleSolution>();
            var seen = new HashSet<string>(); // 用模块uuid组合去重

            int attempts = 0;
            int maxAttempts = MaxSolutions * 1;

            while (solutions.Count < MaxSolutions && attempts < maxAttempts)
            {
                attempts++;

                var init = GreedyConstructSolution(candidates); // this? whats this
                // TODO come back here eventually?

                if (init == null) continue;

                var improved = LocalSearchImprove(init, candidates); // or this?

                var ids = string.Join("|", improved.Modules.Select(m => m.Uuid).OrderBy(s => s));
                if (seen.Add(ids))
                {
                    solutions.Add(improved);
                }
            }
            // ModuleOptimizer.OptimizeModules 末尾
            // OptimizeModules(...) 返回前
            // 末尾排序处：改成根据 sortMode 排序

            List<ModuleSolution> ordered;
            if (sortMode == SortMode.ByTotalAttr)
            {
                // “按最高属性等级排序”：先看勾选属性最高等级 → 再看总属性值 → 再看战力
                ordered = solutions
                    .OrderByDescending(s => s.PriorityLevel)
                    .ThenByDescending(s => s.TotalAttrValue)
                    .ThenByDescending(s => s.Score)
                    .ToList();
            }
            else // SortMode.ByScore
            {
                ordered = solutions
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.PriorityLevel)
                .ThenByDescending(s => s.TotalAttrValue)
                .ToList();

                //Debug.WriteLine("");
                // “按综合评分排序（战力）”：先战力 → 再勾选属性最高等级 → 再总属性值
                //Debug.WriteLine(solutions[0].Score); // every calculate solutions might have different modules
                // ordered = solutions.OrderByDescending(s => s.Score).ToList(); // debug, from here ordered list changes whats inside
                //Debug.WriteLine(ordered[0].Score);
                //ordered = solutions.OrderByDescending(s => s.PriorityLevel).ToList();
                //Debug.WriteLine(ordered[0].Score);
                //ordered = solutions.OrderByDescending(s => s.TotalAttrValue).ToList();
            }
            return ordered.Take(topN).ToList();

            //TODO why is it non deterministic? greedyconstruct and localsearch need to investigate.

        }

        // ====== 打印/展示 ======
        public void PrintSolutionDetails(ModuleSolution solution, int rank)
        {
            if (solution == null) return;

            Console.WriteLine($"\n=== No.{rank}Name collocation ===");
            LogResult($"\n=== No.{rank}Name collocation ===");

            int totalValue = solution.AttrBreakdown.Values.Sum();
            Console.WriteLine($"Total Value: {totalValue}");
            LogResult($"Total Value: {totalValue}");

            Console.WriteLine($"Solution Score: {solution.Score:F2}");
            LogResult($"Solution Score: {solution.Score:F2}");

            Console.WriteLine("\nModule List:");
            LogResult("\nModule List:");
            for (int i = 0; i < solution.Modules.Count; i++)
            {
                var m = solution.Modules[i];
                var partsStr = string.Join(", ", m.Parts.Select(p => $"{p.Name}+{p.Value}"));
                Console.WriteLine($"  {i + 1}. {m.Name} (Quality{m.Quality}) - {partsStr}");
                LogResult($"  {i + 1}. {m.Name} (Quality{m.Quality}) - {partsStr}");
            }

            Console.WriteLine("\nAttribute Distro:");
            LogResult("\nAttribute Distro:");
            foreach (var kv in solution.AttrBreakdown.OrderBy(k => k.Key))
            {
                Console.WriteLine($"  {kv.Key}: +{kv.Value}");
                LogResult($"  {kv.Key}: +{kv.Value}");
            }
        }

        public void OptimizeAndDisplay(
            IReadOnlyList<IModuleInfo> modules,
            ModuleCategory category = ModuleCategory.ALL,
            int topN = 40)
        {
            Console.WriteLine(new string('=', 50));
            LogResult(new string('=', 50));

            Console.WriteLine($"Module combination optimization - {category}");
            LogResult($"Module combination optimization - {category}");

            Console.WriteLine(new string('=', 50));
            LogResult(new string('=', 50));

            var optimal = OptimizeModules(modules, category, topN, SortMode.ByScore);

            if (optimal.Count == 0)
            {
                Console.WriteLine($"Not Found{category}Effective combination of types");
                LogResult($"Not Found{category}Effective combination of types");
                return;
            }

            Console.WriteLine($"\nFound{optimal.Count}Optimal Match:");
            LogResult($"\nFound{optimal.Count}Optimal Match:");

            for (int i = 0; i < optimal.Count; i++)
                PrintSolutionDetails(optimal[i], i + 1);

            Console.WriteLine($"\n{new string('=', 50)}");
            LogResult($"\n{new string('=', 50)}");

            Console.WriteLine("Statistics:");
            LogResult("Statistics:");

            Console.WriteLine($"Total number of modules: {modules.Count}");
            LogResult($"Total number of modules: {modules.Count}");

            int typeCount = modules.Count(m => GetModuleCategory(m) == category);
            Console.WriteLine($"{category} Type module: {typeCount}");
            LogResult($"{category} Type module: {typeCount}");

            Console.WriteLine($"highest combat effectiveness: {optimal[0].Score:F2}");
            LogResult($"highest combat effectiveness: {optimal[0].Score:F2}");

            Console.WriteLine(new string('=', 50));
            LogResult(new string('=', 50));
        }
    }
}
