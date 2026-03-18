# Voice Chat Duplex Upgrade Plan

## Current State: Half-Duplex Voice Chat

### Current Implementation
- **Half-duplex:** Only one person can speak at a time
- **Push-to-talk or toggle-to-talk** mechanism
- **Simple Web Audio API implementation**
- **Basic audio streaming between players**

### Limitations
- No natural conversation flow
- Players must wait for silence to speak
- Awkward interruptions and timing issues
- Not suitable for fast-paced game communication

## Target State: Full-Duplex Voice Chat

### Desired Features
- **Full-duplex:** Multiple players can speak simultaneously
- **Natural conversation flow**
- **Echo cancellation**
- **Noise suppression**
- **Automatic gain control**
- **Low latency audio streaming**

## Technical Implementation Plan

### Phase 1: Audio Processing Enhancement

#### 1.1 Web Audio API Upgrades
```javascript
// Current: Simple audio capture
navigator.mediaDevices.getUserMedia({ audio: true })

// Target: Advanced audio processing
navigator.mediaDevices.getUserMedia({
  audio: {
    echoCancellation: true,
    noiseSuppression: true,
    autoGainControl: true,
    sampleRate: 48000,
    channelCount: 1
  }
})
```

#### 1.2 Audio Context Management
```javascript
// Implement proper audio context for full-duplex
const audioContext = new AudioContext();
const sourceNode = audioContext.createMediaStreamSource(localStream);
const processorNode = audioContext.createScriptProcessor(4096, 1, 1);

// Add audio processing nodes for echo cancellation
const gainNode = audioContext.createGain();
const analyserNode = audioContext.createAnalyser();
```

### Phase 2: Real-Time Audio Streaming

#### 2.1 WebRTC Implementation
```javascript
// Replace current simple streaming with WebRTC
const peerConnection = new RTCPeerConnection({
  iceServers: [
    { urls: 'stun:stun.l.google.com:19302' },
    { urls: 'turn:your-turn-server.com', username: 'user', credential: 'pass' }
  ]
});

// Add audio tracks to peer connection
localStream.getAudioTracks().forEach(track => {
  peerConnection.addTrack(track, localStream);
});
```

#### 2.2 SignalR Integration for WebRTC
```csharp
// Backend: WebRTC signaling through SignalR
public class VoiceChatHub : Hub
{
    public async Task SendOffer(string gameId, string offer)
    {
        await Clients.OthersInGroup(gameId).SendAsync("ReceiveOffer", offer);
    }
    
    public async Task SendAnswer(string gameId, string answer)
    {
        await Clients.OthersInGroup(gameId).SendAsync("ReceiveAnswer", answer);
    }
    
    public async Task SendIceCandidate(string gameId, string candidate)
    {
        await Clients.OthersInGroup(gameId).SendAsync("ReceiveIceCandidate", candidate);
    }
}
```

### Phase 3: Multi-Participant Audio Mixing

#### 3.1 Audio Mixing Logic
```javascript
class AudioMixer {
  constructor() {
    this.participants = new Map();
    this.audioContext = new AudioContext();
  }
  
  addParticipant(userId, stream) {
    const sourceNode = this.audioContext.createMediaStreamSource(stream);
    const gainNode = this.audioContext.createGain();
    
    gainNode.gain.value = 1.0 / (this.participants.size + 1);
    
    sourceNode.connect(gainNode);
    gainNode.connect(this.audioContext.destination);
    
    this.participants.set(userId, { sourceNode, gainNode });
  }
  
  adjustVolume(userId, volume) {
    const participant = this.participants.get(userId);
    if (participant) {
      participant.gainNode.gain.value = volume;
    }
  }
}
```

#### 3.2 Backend Audio Routing
```csharp
public class VoiceChatService
{
    private readonly Dictionary<string, List<string>> _gameParticipants = new();
    
    public async Task JoinVoiceChat(string gameId, string userId)
    {
        if (!_gameParticipants.ContainsKey(gameId))
        {
            _gameParticipants[gameId] = new List<string>();
        }
        
        _gameParticipants[gameId].Add(userId);
        
        // Notify all participants about new user
        await NotifyParticipantsChanged(gameId);
    }
    
    public async Task RouteAudioPacket(string gameId, string fromUserId, byte[] audioData)
    {
        var participants = _gameParticipants.GetValueOrDefault(gameId, new List<string>());
        
        foreach (var participantId in participants)
        {
            if (participantId != fromUserId)
            {
                await SendAudioToParticipant(participantId, audioData);
            }
        }
    }
}
```

### Phase 4: Quality and Performance Optimization

#### 4.1 Adaptive Bitrate
```javascript
class AdaptiveAudioEncoder {
  constructor() {
    this.currentBitrate = 64000; // 64 kbps default
    this.qualityMonitor = new AudioQualityMonitor();
  }
  
  adaptBitrate(networkQuality) {
    if (networkQuality.excellent) {
      this.currentBitrate = 128000; // 128 kbps
    } else if (networkQuality.good) {
      this.currentBitrate = 64000;  // 64 kbps
    } else if (networkQuality.poor) {
      this.currentBitrate = 32000;  // 32 kbps
    }
  }
}
```

#### 4.2 Buffer Management
```javascript
class AudioBuffer {
  constructor(targetLatency = 100) { // 100ms target
    this.targetLatency = targetLatency;
    this.buffers = new Map();
  }
  
  addAudio(userId, audioData) {
    if (!this.buffers.has(userId)) {
      this.buffers.set(userId, new CircularBuffer(10));
    }
    
    this.buffers.get(userId).push(audioData);
    this.adjustPlaybackRate();
  }
  
  adjustPlaybackRate() {
    // Dynamically adjust playback rate to maintain target latency
    const currentLatency = this.calculateCurrentLatency();
    const adjustment = this.targetLatency / currentLatency;
    
    // Apply adjustment to all audio sources
    this.applyPlaybackRateAdjustment(adjustment);
  }
}
```

### Phase 5: UI/UX Enhancements

#### 5.1 Voice Activity Detection
```javascript
class VoiceActivityDetector {
  constructor(threshold = 0.01) {
    this.threshold = threshold;
    this.isSpeaking = false;
  }
  
  detectVoice(audioBuffer) {
    const energy = this.calculateAudioEnergy(audioBuffer);
    const wasSpeaking = this.isSpeaking;
    this.isSpeaking = energy > this.threshold;
    
    if (this.isSpeaking !== wasSpeaking) {
      this.onSpeakingStateChanged(this.isSpeaking);
    }
  }
  
  onSpeakingStateChanged(isSpeaking) {
    // Update UI to show who is speaking
    updateSpeakingIndicator(isSpeaking);
  }
}
```

#### 5.2 Visual Indicators
```html
<!-- Enhanced voice chat UI -->
<div class="voice-chat-panel">
  <div class="participants">
    @foreach (var participant in Participants)
    {
      <div class="participant @(participant.IsSpeaking ? "speaking" : "")">
        <img src="@participant.Avatar" alt="@participant.Name" />
        <span>@participant.Name</span>
        <div class="volume-indicator" style="width: @(participant.Volume * 100)%"></div>
        <div class="speaking-indicator" style="display: @(participant.IsSpeaking ? "block" : "none")">🎤</div>
      </div>
    }
  </div>
  
  <div class="controls">
    <button class="mute-btn @(IsMuted ? "muted" : "")" @onclick="ToggleMute">
      @(IsMuted ? "🔇" : "🎤")
    </button>
    <input type="range" min="0" max="100" value="@OutputVolume" @oninput="SetOutputVolume" />
    <button class="settings-btn" @onclick="ShowVoiceSettings">⚙️</button>
  </div>
</div>
```

### Phase 6: Testing and Quality Assurance

#### 6.1 Network Condition Testing
```javascript
// Simulate various network conditions
class NetworkSimulator {
  simulatePoorNetwork() {
    // Add latency, packet loss, jitter
    this.addLatency(200); // 200ms delay
    this.addPacketLoss(0.05); // 5% packet loss
    this.addJitter(50); // 50ms jitter
  }
  
  simulateExcellentNetwork() {
    // Minimal latency, no packet loss
    this.addLatency(20); // 20ms delay
    this.addPacketLoss(0); // 0% packet loss
  }
}
```

#### 6.2 Performance Monitoring
```csharp
public class VoiceChatMetrics
{
    public double AverageLatency { get; set; }
    public double PacketLossRate { get; set; }
    public int ActiveParticipants { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    
    public void LogMetrics()
    {
        // Send metrics to monitoring service
        _logger.LogInformation($"Voice Chat Metrics: Latency={AverageLatency}ms, " +
                            $"PacketLoss={PacketLossRate:P}, " +
                            $"Participants={ActiveParticipants}");
    }
}
```

## Implementation Timeline

### Sprint 1 (2 weeks): Foundation
- [ ] WebRTC peer connection setup
- [ ] Basic audio capture with enhanced settings
- [ ] SignalR integration for signaling
- [ ] Simple two-person full-duplex test

### Sprint 2 (2 weeks): Multi-Participant
- [ ] Audio mixing implementation
- [ ] Multi-participant WebRTC connections
- [ ] Voice activity detection
- [ ] Basic visual indicators

### Sprint 3 (2 weeks): Quality & Performance
- [ ] Echo cancellation implementation
- [ ] Noise suppression
- [ ] Adaptive bitrate
- [ ] Buffer management

### Sprint 4 (1 week): Polish & Testing
- [ ] UI/UX enhancements
- [ ] Network condition testing
- [ ] Performance optimization
- [ ] Documentation and deployment

## Technical Challenges & Solutions

### Challenge 1: Echo Cancellation
**Solution:** Use Web Audio API's built-in echo cancellation + custom algorithms

### Challenge 2: Network Latency
**Solution:** Adaptive buffering + jitter buffer management

### Challenge 3: CPU Usage
**Solution:** Efficient audio processing + WebAssembly for heavy computations

### Challenge 4: Browser Compatibility
**Solution:** Feature detection + fallbacks for older browsers

## Success Metrics

### Technical Metrics
- Latency < 150ms (target: 100ms)
- Packet loss < 2%
- CPU usage < 15% per participant
- Memory usage < 50MB for 4 participants

### User Experience Metrics
- Natural conversation flow
- No noticeable echo
- Clear audio quality
- Smooth multi-participant conversations

## Rollout Plan

### Phase 1: Beta Testing
- Enable for selected users only
- Collect feedback and metrics
- Iterate based on issues

### Phase 2: Gradual Rollout
- Enable for 10% of users
- Monitor performance
- Scale up gradually

### Phase 3: Full Release
- Enable for all users
- Provide migration guide
- Monitor and optimize

---

## Files to Modify

### Frontend
- `Components/DraftsGame.razor` - Voice chat UI
- `wwwroot/js/voice.js` - Audio processing logic
- `wwwroot/css/voice.css` - Voice chat styling

### Backend
- `Services/VoiceChatService.cs` - Voice chat backend logic
- `Hubs/VoiceChatHub.cs` - SignalR hub for voice signaling
- `Controllers/VoiceChatController.cs` - REST API for voice settings

### Configuration
- `appsettings.json` - Voice chat configuration
- `Program.cs` - Service registration

---

## Status: Planned for Future Implementation

This upgrade is planned for a future sprint when resources are available. The current half-duplex implementation will remain functional until the full-duplex system is fully tested and ready for deployment.
