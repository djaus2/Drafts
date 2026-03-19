using System.ComponentModel.DataAnnotations;

namespace Drafts.Data;

public sealed class AppUser
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(512)]
    public string Roles { get; set; } = "";

    [Required]
    public byte[] PinSalt { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] PinHash { get; set; } = Array.Empty<byte>();

    [MaxLength(1024)]
    public string? PreferredTtsVoice { get; set; }

    [MaxLength(16)]
    public string? PreferredTtsLanguage { get; set; }

    [MaxLength(16)]
    public string? PreferredTtsRegion { get; set; }

    [MaxLength(2048)]
    public string? VoiceSettings { get; set; }
}
