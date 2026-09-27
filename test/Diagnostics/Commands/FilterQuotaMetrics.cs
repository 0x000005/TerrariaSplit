using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSplit.Diagnostics;

internal static class FilterQuotaMetrics
{
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0 || args[0] != "filter-quota") return false;
        try { RunAsync(args).GetAwaiter().GetResult(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        return true;
    }

    private static async Task RunAsync(string[] args)
    {
        // group: pyramid / shallow / screenshot; depth is explicit for shallow.
        string group = args[1]; int depth = int.Parse(args[2], CultureInfo.InvariantCulture);
        string output = Path.GetFullPath(args[3]); Directory.CreateDirectory(output);
        int target = group switch { "pyramid" => 100, "shallow" => 30, "screenshot" => 5, _ => throw new ArgumentException("Unknown group") };
        var settings = new AutoCreateWorldSettings {
            EnableCheats = true, EnablePyramidFilter = true,
            WorldSize = AutoCreateWorldSize.Small, WorldEvil = AutoCreateWorldEvil.Crimson,
            WorldDifficulty = AutoCreateWorldDifficulty.Classic,
            PyramidFilterItemMask = group == "pyramid" ? 7 : 1,
            PyramidFilterCoinPileMinimum = group == "pyramid" ? 0 : 1,
            PyramidMaximumDepth = group == "pyramid" ? 0 : group == "shallow" ? depth : 30,
            RequireCrimsonBetweenDungeonAndSpawn = group == "screenshot",
            CrimsonDistance = AutoCreateCrimsonDistance.Medium,
            JungleRouteDepth = group == "screenshot" ? AutoCreateJungleRouteDepth.Deep : AutoCreateJungleRouteDepth.None
        };
        var requirements = WorldSeedFilterEvaluator.RequestedRequirements(settings);
        string library = args[4];
        var native = new JungleSeedJudgeNativeClient(library);
        using var current = new WorldSeedFilterEvaluator(native);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, Converters = {new JsonStringEnumConverter()} };
        var lineOptions = new JsonSerializerOptions(options) { WriteIndented = false };
        var states = new[] { new State("current"), new State("withoutPrescreen") };
        File.WriteAllText(Path.Combine(output,"input.json"),JsonSerializer.Serialize(new { group,target,requirements,mode="Classic",requestedThreads=0,library,seedFormula="(index * 747796405 + 2891336453) & 0x7fffffff; index starts 0",clickMsEstimate=243.68119872611464 },options));
        using var log = new StreamWriter(Path.Combine(output,"calls.jsonl"),append:false) { AutoFlush = true };
        for(int index=0;states.Any(s=>s.Accepted<target);index++)
        {
            string seed = (unchecked((uint)index*747796405u+2891336453u)&0x7fffffffu).ToString(CultureInfo.InvariantCulture);
            foreach(int slot in index%2==0 ? new[]{0,1} : new[]{1,0})
            {
                var state=states[slot]; if(state.Accepted>=target)continue;
                var watch=Stopwatch.StartNew(); bool accepted; string reason; int? pass=null;
                if(slot==0)
                {
                    var result=await current.EvaluateAsync(settings,seed,TerrariaWorldGenerationVersion.Modern1458,CancellationToken.None);
                    accepted=result.AcceptSeed; reason=result.Detail; pass=result.Judge?.CheckpointPassIndex;
                    if(!result.CanUsePrediction || result.IsCandidateFailure)state.Errors++;
                }
                else
                {
                    try
                    {
                        var result=await native.AnalyzeAsync(seed,JungleSeedJudgeGameMode.Classic,CancellationToken.None,requirements,threads:0);
                        accepted=result.Decision==JungleSeedJudgeDecision.Accepted;reason=result.Reason??result.Detail??result.Status.ToString();pass=result.CheckpointPassIndex;
                        if(!result.Complete)state.Errors++;
                    }
                    catch(TimeoutException ex) { accepted=false;reason=ex.Message;state.Errors++; }
                }
                double elapsed=watch.Elapsed.TotalMilliseconds;state.ComputeMs+=elapsed;state.Attempts++;
                if(accepted){state.Accepted++;state.Seeds.Add(seed);}
                log.WriteLine(JsonSerializer.Serialize(new { index,seed,route=state.Name,accepted,reason,pass,elapsedMs=elapsed },lineOptions));
                if(accepted || state.Attempts%100==0)
                    Console.WriteLine($"{group} {state.Name}: {state.Accepted}/{target}, attempts={state.Attempts}, compute={state.ComputeMs/1000:F1}s");
            }
            File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(new { group,target,complete=states.All(s=>s.Accepted>=target),states },options));
        }
    }
    private sealed class State(string name)
    {
        public string Name {get;}=name;
        public int Attempts {get;set;}
        public int Accepted {get;set;}
        public int Errors {get;set;}
        public double ComputeMs {get;set;}
        public double EstimatedUiMs=>Attempts*243.68119872611464;
        public double EstimatedTotalMs=>ComputeMs+EstimatedUiMs;
        public List<string> Seeds {get;}=[];
    }
}
