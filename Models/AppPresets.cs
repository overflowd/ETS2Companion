namespace ETS2CompanionLauncher.Models;

public static class AppPresets
{
    public static IReadOnlyList<CompanionApp> GetPopularPresets() => new List<CompanionApp>
    {
        new()
        {
            Id = "preset-lossless-scaling",
            Name = "Lossless Scaling",
            ProcessName = "LosslessScaling",
            LaunchDelaySeconds = 3,
            SearchHints = new List<string> { "LosslessScaling", "Lossless Scaling" },
            SteamAppId = "993090"
        },
        new()
        {
            Id = "preset-beam-eye-tracker",
            Name = "Beam Eye Tracker",
            ProcessName = "BeamEyeTracker",
            LaunchDelaySeconds = 1,
            SearchHints = new List<string> { "BeamEyeTracker", "Beam Eye Tracker", "EyewareBeam" },
            SteamAppId = "2375780"
        },
        new()
        {
            Id = "preset-lookpilot",
            Name = "LookPilot",
            ProcessName = "LookPilot",
            LaunchDelaySeconds = 1,
            SearchHints = new List<string> { "LookPilot" }
        },
        new()
        {
            Id = "preset-westplay",
            Name = "WestPlay",
            ProcessName = "WestPlay",
            LaunchDelaySeconds = 1,
            SearchHints = new List<string> { "WestPlay" }
        },
        new()
        {
            Id = "preset-opentrack",
            Name = "OpenTrack",
            ProcessName = "opentrack",
            LaunchDelaySeconds = 1,
            SearchHints = new List<string> { "opentrack", "OpenTrack" }
        },
        new()
        {
            Id = "preset-trackir",
            Name = "TrackIR",
            ProcessName = "TrackIR5",
            LaunchDelaySeconds = 1,
            SearchHints = new List<string> { "TrackIR5", "TrackIR" }
        },
        new()
        {
            Id = "preset-simhub",
            Name = "SimHub",
            ProcessName = "SimHubWPF",
            LaunchDelaySeconds = 2,
            SearchHints = new List<string> { "SimHubWPF", "SimHub" }
        },
        new()
        {
            Id = "preset-truckersmp",
            Name = "TruckersMP",
            ProcessName = "TruckersMP-Launcher",
            LaunchDelaySeconds = 0,
            SearchHints = new List<string> { "TruckersMP" }
        },
        new()
        {
            Id = "preset-trucksbook",
            Name = "TrucksBook Client",
            ProcessName = "TBClient",
            LaunchDelaySeconds = 1,
            SearchHints = new List<string> { "TBClient", "TrucksBook" }
        },
        new()
        {
            Id = "preset-crewchief",
            Name = "CrewChief",
            ProcessName = "CrewChiefV4",
            LaunchDelaySeconds = 2,
            SearchHints = new List<string> { "CrewChiefV4", "CrewChief" }
        },
        new()
        {
            Id = "preset-obs",
            Name = "OBS Studio",
            ProcessName = "obs64",
            LaunchDelaySeconds = 2,
            SearchHints = new List<string> { "obs64", "OBS Studio" }
        },
        new()
        {
            Id = "preset-discord",
            Name = "Discord",
            ProcessName = "Discord",
            LaunchDelaySeconds = 0,
            SearchHints = new List<string> { "Discord" }
        }
    };
}
