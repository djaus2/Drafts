using System.ComponentModel.DataAnnotations;

namespace Drafts.Data;

public sealed class AppSettings
{
    public int Id { get; set; } = 1;

    [Required]
    public int MaxTimeoutMins { get; set; } = 30;

    [Required]
    public int ReaperPeriodSeconds { get; set; } = 30;

    [Required]
    public string LastMoveHighlightColor { get; set; } = "rgba(255,0,0,0.85)";

    [Required]
    public bool EntrapmentMode { get; set; } = true;
}
