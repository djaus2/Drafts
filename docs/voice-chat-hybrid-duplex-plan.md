# Hybrid Voice Chat: Full-Duplex with Hold-to-Talk

## Concept: Full-Duplex Audio with Manual Transmission Control

### Key Insight
- **Full-duplex audio processing** = Multiple people can be heard simultaneously
- **Hold-to-talk control** = Manual transmission gating
- **Result:** Natural conversation flow with intentional speaking

## Current vs Target State

### Current: Half-Duplex + Hold-to-Talk
- Only one person can transmit at a time
- Simple audio streaming
- Basic push-to-talk mechanism

### Target: Full-Duplex + Hold-to-Talk
- Multiple people can transmit simultaneously
- Advanced audio processing and mixing
- Enhanced hold-to-talk with visual feedback

## Technical Implementation

### Phase 1: Enhanced Audio Processing

#### 1.1 Advanced Audio Capture
```javascript
// Enhanced audio capture with processing
navigator.mediaDevices.getUserMedia({
  audio: {
    echoCancellation: true,
    noiseSuppression: true,
    autoGainControl: true,
    sampleRate: 48000,
    channelCount: 1
  }
}).then(stream => {
  localAudioStream = stream;
  setupAudioProcessing(stream);
});
```

#### 1.2 Audio Context Setup
```javascript
class VoiceChatProcessor {
  constructor() {
    this.audioContext = new AudioContext();
    this.isTransmitting = false;
    this.participantStreams = new Map();
  }
  
  setupAudioProcessing(localStream) {
    // Create audio processing chain
    this.sourceNode = this.audioContext.createMediaStreamSource(localStream);
    this.gainNode = this.audioContext.createGain();
    this.analyserNode = this.audioContext.createAnalyser();
    
    // Setup processing chain
    this.sourceNode.connect(this.gainNode);
    this.gainNode.connect(this.analyserNode);
    
    // Voice activity detection
    this.setupVoiceActivityDetection();
  }
  
  startTransmitting() {
    this.isTransmitting = true;
    this.gainNode.gain.value = 1.0;
    this.beginAudioStreaming();
  }
  
  stopTransmitting() {
    this.isTransmitting = false;
    this.gainNode.gain.value = 0.0;
    this.stopAudioStreaming();
  }
}
```

### Phase 2: Multi-Participant Audio Mixing

#### 2.1 Audio Mixer Implementation
```javascript
class AudioMixer {
  constructor() {
    this.audioContext = new AudioContext();
    this.participants = new Map();
    this.outputGainNode = this.audioContext.createGain();
    this.outputGainNode.connect(this.audioContext.destination);
  }
  
  addParticipant(userId, audioStream) {
    const sourceNode = this.audioContext.createMediaStreamSource(audioStream);
    const gainNode = this.audioContext.createGain();
    const pannerNode = this.audioContext.createStereoPanner();
    
    // Create processing chain
    sourceNode.connect(gainNode);
    gainNode.connect(pannerNode);
    pannerNode.connect(this.outputGainNode);
    
    // Store participant nodes
    this.participants.set(userId, {
      sourceNode,
      gainNode,
      pannerNode,
      isActive: false
    });
    
    // Auto-adjust volumes for multiple participants
    this.adjustParticipantVolumes();
  }
  
  adjustParticipantVolumes() {
    const activeCount = Array.from(this.participants.values())
      .filter(p => p.isActive).length;
    
    const volumePerParticipant = activeCount > 0 ? 1.0 / activeCount : 1.0;
    
    this.participants.forEach((participant, userId) => {
      if (participant.isActive) {
        participant.gainNode.gain.value = volumePerParticipant;
      }
    });
  }
  
  setParticipantActive(userId, isActive) {
    const participant = this.participants.get(userId);
    if (participant) {
      participant.isActive = isActive;
      this.adjustParticipantVolumes();
    }
  }
}
```

#### 2.2 Enhanced Hold-to-Talk Logic
```javascript
class EnhancedHoldToTalk {
  constructor(voiceChatProcessor, audioMixer) {
    this.voiceProcessor = voiceChatProcessor;
    this.mixer = audioMixer;
    this.isPressed = false;
    this.debounceTimer = null;
  }
  
  startTalking() {
    // Debounce rapid presses
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }
    
    this.isPressed = true;
    this.voiceProcessor.startTransmitting();
    this.mixer.setParticipantActive('local', true);
    
    // Visual feedback
    this.updateTalkingIndicator(true);
    this.notifyOtherParticipants('startTalking');
  }
  
  stopTalking() {
    // Small delay to prevent accidental cutoffs
    this.debounceTimer = setTimeout(() => {
      this.isPressed = false;
      this.voiceProcessor.stopTransmitting();
      this.mixer.setParticipantActive('local', false);
      
      // Visual feedback
      this.updateTalkingIndicator(false);
      this.notifyOtherParticipants('stopTalking');
    }, 100);
  }
  
  updateTalkingIndicator(isTalking) {
    const indicator = document.querySelector('.talking-indicator');
    if (indicator) {
      indicator.style.display = isTalking ? 'block' : 'none';
    }
  }
}
```

### Phase 3: Real-Time Audio Streaming

#### 3.1 WebRTC with Manual Control
```javascript
class ControlledWebRTCConnection {
  constructor(signalrConnection) {
    this.signalr = signalrConnection;
    this.peerConnection = new RTCPeerConnection({
      iceServers: [
        { urls: 'stun:stun.l.google.com:19302' }
      ]
    });
    this.isTransmitting = false;
    this.localAudioTrack = null;
  }
  
  async initializeConnection(gameId, participants) {
    // Setup WebRTC connections with all participants
    for (const participantId of participants) {
      if (participantId !== this.localUserId) {
        await this.setupPeerConnection(participantId);
      }
    }
  }
  
  async addLocalAudioTrack(audioStream) {
    this.localAudioTrack = audioStream.getAudioTracks()[0];
    
    // Add track but don't transmit until user presses button
    this.peerConnection.addTrack(this.localAudioTrack, audioStream);
    
    // Initially disable the track
    this.localAudioTrack.enabled = false;
  }
  
  startTransmitting() {
    if (this.localAudioTrack) {
      this.localAudioTrack.enabled = true;
      this.isTransmitting = true;
    }
  }
  
  stopTransmitting() {
    if (this.localAudioTrack) {
      this.localAudioTrack.enabled = false;
      this.isTransmitting = false;
    }
  }
}
```

#### 3.2 SignalR Integration
```csharp
public class VoiceChatHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> GameParticipants = new();
    
    public async Task JoinVoiceChat(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        
        if (!GameParticipants.ContainsKey(gameId))
        {
            GameParticipants[gameId] = new HashSet<string>();
        }
        GameParticipants[gameId].Add(Context.ConnectionId);
        
        // Notify others about new participant
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantJoined", Context.ConnectionId);
    }
    
    public async Task StartTalking(string gameId)
    {
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantStartedTalking", Context.ConnectionId);
    }
    
    public async Task StopTalking(string gameId)
    {
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantStoppedTalking", Context.ConnectionId);
    }
    
    public async Task LeaveVoiceChat(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
        
        if (GameParticipants.ContainsKey(gameId))
        {
            GameParticipants[gameId].Remove(Context.ConnectionId);
        }
        
        await Clients.OthersInGroup(gameId).SendAsync("ParticipantLeft", Context.ConnectionId);
    }
}
```

### Phase 4: Enhanced UI/UX

#### 4.1 Visual Talking Indicators
```html
<!-- Enhanced voice chat UI -->
<div class="voice-chat-controls">
  <div class="participants-grid">
    @foreach (var participant in Participants)
    {
      <div class="participant @(participant.IsTalking ? "talking" : "")">
        <div class="participant-avatar">
          <img src="@participant.Avatar" alt="@participant.Name" />
          <div class="talking-indicator @(participant.IsTalking ? "active" : "")">
            <div class="sound-waves"></div>
          </div>
        </div>
        <div class="participant-info">
          <span class="participant-name">@participant.Name</span>
          <div class="volume-bar" style="width: @(participant.Volume * 100)%"></div>
        </div>
      </div>
    }
  </div>
  
  <div class="talk-controls">
    <button class="talk-button @(isTalking ? "active" : "")" 
            @onmousedown="StartTalking" 
            @onmouseup="StopTalking"
            @onmouseleave="StopTalking"
            @ontouchstart="StartTalking"
            @ontouchend="StopTalking">
      <div class="talk-icon">🎤</div>
      <div class="talk-text">@(isTalking ? "Talking..." : "Hold to Talk")</div>
    </button>
    
    <div class="voice-settings">
      <button class="mute-btn @(isMuted ? "muted" : "")" @onclick="ToggleMute">
        @(isMuted ? "🔇" : "🔊")
      </button>
      <input type="range" min="0" max="100" value="@outputVolume" 
             @oninput="SetOutputVolume" class="volume-slider" />
    </div>
  </div>
</div>
```

#### 4.2 CSS for Talking Indicators
```css
.participant-avatar {
  position: relative;
  width: 60px;
  height: 60px;
  border-radius: 50%;
  overflow: hidden;
}

.talking-indicator {
  position: absolute;
  top: -5px;
  right: -5px;
  width: 20px;
  height: 20px;
  background: #4CAF50;
  border-radius: 50%;
  display: none;
  align-items: center;
  justify-content: center;
}

.talking-indicator.active {
  display: flex;
  animation: pulse 1.5s infinite;
}

.sound-waves {
  width: 12px;
  height: 12px;
  background: white;
  border-radius: 50%;
  animation: soundWave 0.8s infinite;
}

@keyframes pulse {
  0% { transform: scale(1); opacity: 1; }
  50% { transform: scale(1.2); opacity: 0.8; }
  100% { transform: scale(1); opacity: 1; }
}

@keyframes soundWave {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.3); }
}

.talk-button {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border: none;
  border-radius: 50px;
  padding: 20px 40px;
  color: white;
  font-weight: bold;
  cursor: pointer;
  transition: all 0.3s ease;
  user-select: none;
  -webkit-user-select: none;
}

.talk-button:active,
.talk-button.active {
  background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
  transform: scale(0.95);
  box-shadow: 0 0 20px rgba(245, 87, 108, 0.5);
}
```

### Phase 5: Enhanced Features

#### 5.1 Voice Activity Detection (Optional Enhancement)
```javascript
class VoiceActivityDetector {
  constructor(threshold = 0.01) {
    this.threshold = threshold;
    this.isVoiceDetected = false;
    this.confidence = 0;
  }
  
  analyzeAudio(audioBuffer) {
    const energy = this.calculateEnergy(audioBuffer);
    const zeroCrossingRate = this.calculateZeroCrossingRate(audioBuffer);
    
    // Combine multiple features for better detection
    this.confidence = this.calculateConfidence(energy, zeroCrossingRate);
    this.isVoiceDetected = this.confidence > this.threshold;
    
    return {
      detected: this.isVoiceDetected,
      confidence: this.confidence,
      energy: energy
    };
  }
  
  calculateConfidence(energy, zeroCrossingRate) {
    // Weighted combination of features
    const energyWeight = 0.7;
    const zcrWeight = 0.3;
    
    return (energy * energyWeight) + (zeroCrossingRate * zcrWeight);
  }
}
```

#### 5.2 Automatic Gain Control
```javascript
class AutomaticGainControl {
  constructor(targetLevel = 0.7) {
    this.targetLevel = targetLevel;
    this.currentGain = 1.0;
    this.smoothingFactor = 0.1;
  }
  
  adjustGain(inputLevel) {
    const error = this.targetLevel - inputLevel;
    const gainAdjustment = error * this.smoothingFactor;
    
    this.currentGain += gainAdjustment;
    this.currentGain = Math.max(0.1, Math.min(3.0, this.currentGain));
    
    return this.currentGain;
  }
}
```

## Benefits of Hybrid Approach

### 1. Natural Conversation Flow
- Multiple people can speak simultaneously when needed
- No awkward waiting for silence
- More realistic group conversations

### 2. Intentional Communication
- Hold-to-talk prevents accidental background noise
- Users control when they transmit
- Reduces unnecessary audio traffic

### 3. Better Audio Quality
- Advanced processing reduces echo and noise
- Automatic gain control optimizes volume
- Professional-grade audio mixing

### 4. Enhanced User Experience
- Clear visual feedback for who's talking
- Smooth transitions between speakers
- Intuitive controls

## Implementation Priority

### Phase 1: Core Functionality (2 weeks)
- [ ] Enhanced audio capture
- [ ] Basic multi-participant mixing
- [ ] Hold-to-talk with visual feedback
- [ ] WebRTC streaming with manual control

### Phase 2: Quality Enhancement (1 week)
- [ ] Echo cancellation
- [ ] Noise suppression
- [ ] Automatic gain control
- [ ] Advanced visual indicators

### Phase 3: Polish & Optimization (1 week)
- [ ] Performance optimization
- [ ] Network adaptation
- [ ] UI/UX refinements
- [ ] Testing and bug fixes

## Technical Considerations

### Bandwidth Usage
- **Current:** ~64 kbps per active speaker
- **Target:** ~32-96 kbps per active speaker (adaptive)
- **Optimization:** Only transmit when button pressed

### CPU Usage
- **Audio processing:** ~5-10% CPU
- **WebRTC overhead:** ~2-5% CPU
- **Total target:** <15% CPU for 4 participants

### Latency
- **Target:** <100ms end-to-end
- **Buffer size:** 20-40ms
- **Network adaptation:** Dynamic buffer adjustment

---

## Files to Modify

### Frontend Components
- `Components/DraftsGame.razor` - Enhanced voice chat UI
- `wwwroot/js/voice-chat.js` - Audio processing and WebRTC
- `wwwroot/css/voice-chat.css` - Enhanced styling

### Backend Services
- `Services/VoiceChatService.cs` - Voice chat management
- `Hubs/VoiceChatHub.cs` - SignalR for voice signaling
- `Services/AudioProcessingService.cs` - Audio processing logic

### Configuration
- `appsettings.json` - Voice chat settings
- `Program.cs` - Service registration

---

## Status: Planned Implementation

This hybrid approach provides the best of both worlds: full-duplex audio quality with intentional transmission control through hold-to-talk functionality.
