using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Drafts.Services
{
    public class AdminVoiceSettingsService
    {
        private readonly AuthService _auth;
        private Dictionary<string, object> _cachedAdminSettings = new();

        public AdminVoiceSettingsService(AuthService auth)
        {
            _auth = auth;
        }

        public async Task<Dictionary<string, object>> GetAdminVoiceSettingsAsync()
        {
            try
            {
                // Try to get from cache first
                if (_cachedAdminSettings.Count > 0)
                {
                    Console.WriteLine("[AdminVoiceSettings] Returning cached settings");
                    return new Dictionary<string, object>(_cachedAdminSettings);
                }

                // Load from database
                var settingsJson = await _auth.GetAdminVoiceSettingsAsync();
                Console.WriteLine($"[AdminVoiceSettings] Database settings: {settingsJson}");
                
                if (!string.IsNullOrWhiteSpace(settingsJson))
                {
                    var settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(settingsJson);
                    if (settings != null)
                    {
                        Console.WriteLine($"[AdminVoiceSettings] Loaded {settings.Count} settings from database");
                        _cachedAdminSettings = settings;
                        return new Dictionary<string, object>(settings);
                    }
                }

                // Return default settings if none found
                Console.WriteLine("[AdminVoiceSettings] No database settings found, returning defaults");
                var defaultSettings = GetDefaultVoiceSettings();
                _cachedAdminSettings = defaultSettings;
                return defaultSettings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminVoiceSettings] Error loading settings: {ex.Message}");
                return GetDefaultVoiceSettings();
            }
        }

        public async Task<bool> SaveAdminVoiceSettingsAsync(Dictionary<string, object> settings)
        {
            try
            {
                var settingsJson = System.Text.Json.JsonSerializer.Serialize(settings);
                var success = await _auth.SaveAdminVoiceSettingsAsync(settingsJson);
                
                if (success)
                {
                    // Update cache
                    _cachedAdminSettings = new Dictionary<string, object>(settings);
                    Console.WriteLine("[AdminVoiceSettings] Settings saved successfully");
                }
                else
                {
                    Console.WriteLine("[AdminVoiceSettings] Failed to save settings");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminVoiceSettings] Error saving settings: {ex.Message}");
                return false;
            }
        }

        public void ClearCache()
        {
            _cachedAdminSettings.Clear();
        }

        private Dictionary<string, object> GetDefaultVoiceSettings()
        {
            return new Dictionary<string, object>
            {
                { "echoCancellationEnabled", true },
                { "noiseSuppressionEnabled", true },
                { "autoGainControlEnabled", true },
                { "inputSensitivity", 75 },
                { "adaptiveBitrateEnabled", true },
                { "qualityPriority", "quality" },
                { "useEnhancedVoiceChat", false }  // Default to classic voice system
            };
        }
    }
}
