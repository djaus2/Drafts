using System.ComponentModel.DataAnnotations;

namespace Drafts.Data;

public sealed class AppSettings
{
    public int Id { get; set; } = 1;

    [Required]
    public int MaxMoveTimeoutMins { get; set; } = 5;

    [Required]
    public int MaxGameTimeMins { get; set; } = 30;

    [Required]
    public int MaxGameStartWaitTimeMins { get; set; } = 30;

    [Required]
    public int MaxLoginHrs { get; set; } = 4;

    [Required]
    public int ReaperPeriodSeconds { get; set; } = 30;

    [Required]
    public string LastMoveHighlightColor { get; set; } = "rgba(255,0,0,0.85)";

    [Required]
    public bool EntrapmentMode { get; set; } = true;

    [Required]
    public double MultiJumpGraceSeconds { get; set; } = 1.5;

    [Required]
    public bool GameInitiatorGoesFirst { get; set; } = true;
}
