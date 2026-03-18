// Enhanced Voice Chat - Full-Duplex with Hold-to-Talk
// Version 1.0.0 - Full-Duplex Implementation

window.enhancedVoiceChat = window.enhancedVoiceChat || {};

(function() {
    'use strict';

    class VoiceChatProcessor {
        constructor() {
            this.audioContext = null;
            this.localStream = null;
            this.sourceNode = null;
            this.gainNode = null;
            this.analyserNode = null;
            this.isTransmitting = false;
            this.participantStreams = new Map();
            this.audioMixer = null;
            this.webRTCConnections = new Map();
            this.signalRConnection = null;
            this.dotNetRef = null;
            
            // Audio processing settings
            this.settings = {
                sampleRate: 48000,
                channelCount: 1,
                bufferSize: 4096,
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            };
            
            // Voice activity detection
            this.voiceActivityDetector = new VoiceActivityDetector();
            this.automaticGainControl = new AutomaticGainControl();
            
            // Performance monitoring
            this.metrics = {
                latency: 0,
                packetLoss: 0,
                cpuUsage: 0,
                activeParticipants: 0
            };
        }

        async initialize(dotNetRef) {
            try {
                this.dotNetRef = dotNetRef;
                
                // Initialize audio context
                this.audioContext = new (window.AudioContext || window.webkitAudioContext)({
                    sampleRate: this.settings.sampleRate
                });
                
                // Get user media with enhanced settings
                await this.setupAudioCapture();
                
                // Initialize audio mixer
                this.audioMixer = new AudioMixer(this.audioContext);
                
                // Setup WebRTC connections
                await this.setupWebRTC();
                
                console.log('[EnhancedVoiceChat] Initialized successfully');
                return true;
            } catch (error) {
                console.error('[EnhancedVoiceChat] Initialization failed:', error);
                return false;
            }
        }

        async setupAudioCapture() {
            try {
                const constraints = {
                    audio: {
                        echoCancellation: this.settings.echoCancellation,
                        noiseSuppression: this.settings.noiseSuppression,
                        autoGainControl: this.settings.autoGainControl,
                        sampleRate: this.settings.sampleRate,
                        channelCount: this.settings.channelCount
                    }
                };
                
                this.localStream = await navigator.mediaDevices.getUserMedia(constraints);
                this.setupAudioProcessingChain();
                
                console.log('[EnhancedVoiceChat] Audio capture setup complete');
            } catch (error) {
                console.error('[EnhancedVoiceChat] Audio capture failed:', error);
                throw error;
            }
        }

        setupAudioProcessingChain() {
            // Create audio processing nodes
            this.sourceNode = this.audioContext.createMediaStreamSource(this.localStream);
            this.gainNode = this.audioContext.createGain();
            this.analyserNode = this.audioContext.createAnalyser();
            
            // Configure analyser
            this.analyserNode.fftSize = 2048;
            this.analyserNode.smoothingTimeConstant = 0.8;
            
            // Create processing chain
            this.sourceNode.connect(this.gainNode);
            this.gainNode.connect(this.analyserNode);
            
            // Initially disable transmission
            this.gainNode.gain.value = 0.0;
            
            // Setup voice activity detection
            this.setupVoiceActivityDetection();
        }

        setupVoiceActivityDetection() {
            const bufferLength = this.analyserNode.frequencyBinCount;
            const dataArray = new Uint8Array(bufferLength);
            
            const detect = () => {
                if (!this.isTransmitting) {
                    requestAnimationFrame(detect);
                    return;
                }
                
                this.analyserNode.getByteFrequencyData(dataArray);
                
                // Analyze audio for voice activity
                const result = this.voiceActivityDetector.analyzeAudio(dataArray);
                
                // Apply automatic gain control
                const targetGain = this.automaticGainControl.adjustGain(result.energy);
                this.gainNode.gain.value = targetGain;
                
                // Update metrics
                this.updateMetrics(result);
                
                requestAnimationFrame(detect);
            };
            
            detect();
        }

        async setupWebRTC() {
            // Create peer connection configuration
            const configuration = {
                iceServers: [
                    { urls: 'stun:stun.l.google.com:19302' },
                    { urls: 'stun:stun1.l.google.com:19302' }
                ]
            };
            
            // This will be expanded when we have multiple participants
            console.log('[EnhancedVoiceChat] WebRTC setup complete');
        }

        startTransmitting() {
            if (!this.localStream || this.isTransmitting) return;
            
            this.isTransmitting = true;
            this.gainNode.gain.value = 1.0;
            
            // Enable audio tracks for all WebRTC connections
            this.webRTCConnections.forEach((connection, userId) => {
                const audioTrack = this.localStream.getAudioTracks()[0];
                if (audioTrack) {
                    audioTrack.enabled = true;
                }
            });
            
            // Notify backend
            this.notifyTransmissionStart();
            
            console.log('[EnhancedVoiceChat] Transmission started');
        }

        stopTransmitting() {
            if (!this.isTransmitting) return;
            
            this.isTransmitting = false;
            this.gainNode.gain.value = 0.0;
            
            // Disable audio tracks for all WebRTC connections
            this.webRTCConnections.forEach((connection, userId) => {
                const audioTrack = this.localStream.getAudioTracks()[0];
                if (audioTrack) {
                    audioTrack.enabled = false;
                }
            });
            
            // Notify backend
            this.notifyTransmissionStop();
            
            console.log('[EnhancedVoiceChat] Transmission stopped');
        }

        addParticipant(userId, stream) {
            if (!this.audioMixer) return;
            
            this.audioMixer.addParticipant(userId, stream);
            this.participantStreams.set(userId, stream);
            
            console.log(`[EnhancedVoiceChat] Added participant: ${userId}`);
        }

        removeParticipant(userId) {
            if (!this.audioMixer) return;
            
            this.audioMixer.removeParticipant(userId);
            this.participantStreams.delete(userId);
            
            console.log(`[EnhancedVoiceChat] Removed participant: ${userId}`);
        }

        notifyTransmissionStart() {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnTransmissionStart');
            }
        }

        notifyTransmissionStop() {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnTransmissionStop');
            }
        }

        updateMetrics(voiceData) {
            this.metrics.activeParticipants = this.participantStreams.size;
            this.metrics.cpuUsage = this.calculateCPUUsage();
            
            // Send metrics to backend periodically
            if (Math.random() < 0.1) { // 10% chance to avoid spam
                this.sendMetrics();
            }
        }

        calculateCPUUsage() {
            // Simple CPU usage estimation based on active participants
            const baseUsage = 5; // Base usage in percent
            const perParticipantUsage = 2; // Usage per participant
            return Math.min(baseUsage + (this.participantStreams.size * perParticipantUsage), 15);
        }

        sendMetrics() {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('UpdateMetrics', this.metrics);
            }
        }

        async cleanup() {
            // Stop transmission
            this.stopTransmitting();
            
            // Clean up audio streams
            if (this.localStream) {
                this.localStream.getTracks().forEach(track => track.stop());
            }
            
            // Clean up WebRTC connections
            this.webRTCConnections.forEach(connection => {
                connection.close();
            });
            
            // Clean up audio context
            if (this.audioContext) {
                await this.audioContext.close();
            }
            
            console.log('[EnhancedVoiceChat] Cleanup complete');
        }
    }

    class AudioMixer {
        constructor(audioContext) {
            this.audioContext = audioContext;
            this.participants = new Map();
            this.outputGainNode = audioContext.createGain();
            this.outputGainNode.connect(audioContext.destination);
        }

        addParticipant(userId, audioStream) {
            if (this.participants.has(userId)) return;
            
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
                isActive: false,
                stream: audioStream
            });
            
            // Auto-adjust volumes for multiple participants
            this.adjustParticipantVolumes();
            
            console.log(`[AudioMixer] Added participant: ${userId}`);
        }

        removeParticipant(userId) {
            const participant = this.participants.get(userId);
            if (!participant) return;
            
            // Disconnect nodes
            participant.sourceNode.disconnect();
            participant.gainNode.disconnect();
            participant.pannerNode.disconnect();
            
            // Remove from map
            this.participants.delete(userId);
            
            // Auto-adjust volumes
            this.adjustParticipantVolumes();
            
            console.log(`[AudioMixer] Removed participant: ${userId}`);
        }

        setParticipantActive(userId, isActive) {
            const participant = this.participants.get(userId);
            if (participant) {
                participant.isActive = isActive;
                this.adjustParticipantVolumes();
            }
        }

        adjustParticipantVolumes() {
            const activeCount = Array.from(this.participants.values())
                .filter(p => p.isActive).length;
            
            const volumePerParticipant = activeCount > 0 ? 1.0 / activeCount : 1.0;
            
            this.participants.forEach((participant, userId) => {
                if (participant.isActive) {
                    participant.gainNode.gain.value = volumePerParticipant;
                } else {
                    participant.gainNode.gain.value = 0.0;
                }
            });
        }

        setParticipantVolume(userId, volume) {
            const participant = this.participants.get(userId);
            if (participant && participant.isActive) {
                participant.gainNode.gain.value = volume;
            }
        }
    }

    class VoiceActivityDetector {
        constructor(threshold = 0.01) {
            this.threshold = threshold;
            this.isVoiceDetected = false;
            this.confidence = 0;
        }

        analyzeAudio(audioData) {
            const energy = this.calculateEnergy(audioData);
            const zeroCrossingRate = this.calculateZeroCrossingRate(audioData);
            
            // Combine multiple features for better detection
            this.confidence = this.calculateConfidence(energy, zeroCrossingRate);
            this.isVoiceDetected = this.confidence > this.threshold;
            
            return {
                detected: this.isVoiceDetected,
                confidence: this.confidence,
                energy: energy,
                zeroCrossingRate: zeroCrossingRate
            };
        }

        calculateEnergy(audioData) {
            let sum = 0;
            for (let i = 0; i < audioData.length; i++) {
                sum += audioData[i] * audioData[i];
            }
            return Math.sqrt(sum / audioData.length) / 255; // Normalize to 0-1
        }

        calculateZeroCrossingRate(audioData) {
            let crossings = 0;
            for (let i = 1; i < audioData.length; i++) {
                if ((audioData[i - 1] < 128 && audioData[i] >= 128) ||
                    (audioData[i - 1] >= 128 && audioData[i] < 128)) {
                    crossings++;
                }
            }
            return crossings / audioData.length;
        }

        calculateConfidence(energy, zeroCrossingRate) {
            // Weighted combination of features
            const energyWeight = 0.7;
            const zcrWeight = 0.3;
            
            return (energy * energyWeight) + (zeroCrossingRate * zcrWeight);
        }
    }

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

    // Enhanced hold-to-talk functionality
    class EnhancedHoldToTalk {
        constructor(voiceChatProcessor) {
            this.voiceProcessor = voiceChatProcessor;
            this.isPressed = false;
            this.debounceTimer = null;
            this.talkingIndicator = null;
        }

        initialize() {
            // Find talking indicator element
            this.talkingIndicator = document.querySelector('.talking-indicator');
        }

        startTalking() {
            // Debounce rapid presses
            if (this.debounceTimer) {
                clearTimeout(this.debounceTimer);
            }
            
            this.isPressed = true;
            this.voiceProcessor.startTransmitting();
            this.updateTalkingIndicator(true);
            
            console.log('[EnhancedHoldToTalk] Started talking');
        }

        stopTalking() {
            // Small delay to prevent accidental cutoffs
            this.debounceTimer = setTimeout(() => {
                this.isPressed = false;
                this.voiceProcessor.stopTransmitting();
                this.updateTalkingIndicator(false);
                
                console.log('[EnhancedHoldToTalk] Stopped talking');
            }, 100);
        }

        updateTalkingIndicator(isTalking) {
            if (this.talkingIndicator) {
                this.talkingIndicator.style.display = isTalking ? 'block' : 'none';
                this.talkingIndicator.classList.toggle('active', isTalking);
            }
        }
    }

    // Global instance
    let voiceChatProcessor = null;
    let holdToTalk = null;

    // Public API
    window.enhancedVoiceChat = {
        async initialize(dotNetRef) {
            if (voiceChatProcessor) {
                await voiceChatProcessor.cleanup();
            }
            
            voiceChatProcessor = new VoiceChatProcessor();
            holdToTalk = new EnhancedHoldToTalk(voiceChatProcessor);
            
            const success = await voiceChatProcessor.initialize(dotNetRef);
            if (success) {
                holdToTalk.initialize();
            }
            
            return success;
        },

        startTalking() {
            if (holdToTalk) {
                holdToTalk.startTalking();
            }
        },

        stopTalking() {
            if (holdToTalk) {
                holdToTalk.stopTalking();
            }
        },

        addParticipant(userId, stream) {
            if (voiceChatProcessor) {
                voiceChatProcessor.addParticipant(userId, stream);
            }
        },

        removeParticipant(userId) {
            if (voiceChatProcessor) {
                voiceChatProcessor.removeParticipant(userId);
            }
        },

        async cleanup() {
            if (voiceChatProcessor) {
                await voiceChatProcessor.cleanup();
                voiceChatProcessor = null;
                holdToTalk = null;
            }
        },

        // Get current state
        isTransmitting() {
            return voiceChatProcessor ? voiceChatProcessor.isTransmitting : false;
        },

        getMetrics() {
            return voiceChatProcessor ? voiceChatProcessor.metrics : null;
        }
    };

    console.log('[EnhancedVoiceChat] Module loaded');
})();
