// Advanced Audio Processing - Phase 2 Enhancements
// Version 2.0.0 - Quality Enhancement Features

window.advancedAudioProcessing = window.advancedAudioProcessing || {};

(function() {
    'use strict';

    class AdvancedEchoCancellation {
        constructor(audioContext) {
            this.audioContext = audioContext;
            this.echoDelay = 0.05; // 50ms typical echo delay
            this.echoDecay = 0.7; // Echo decay factor
            this.adaptiveFilter = new AdaptiveFilter();
            this.echoEstimate = null;
            this.processedBuffer = null;
        }

        initialize(sampleRate) {
            this.echoDelay = Math.round(sampleRate * 0.05); // 50ms in samples
            this.processedBuffer = new Float32Array(this.echoDelay);
            this.echoEstimate = new Float32Array(this.echoDelay);
        }

        processAudio(inputBuffer, outputBuffer) {
            const input = inputBuffer.getChannelData(0);
            const output = outputBuffer.getChannelData(0);

            for (let i = 0; i < input.length; i++) {
                // Adaptive echo cancellation
                const echoEstimate = this.estimateEcho(input[i]);
                const cancelled = input[i] - echoEstimate;
                
                // Apply adaptive filter
                const filtered = this.adaptiveFilter.process(cancelled);
                
                // Update echo estimate
                this.updateEchoEstimate(input[i]);
                
                output[i] = filtered;
            }
        }

        estimateEcho(currentSample) {
            // Simple echo estimation using delay line
            const delayedIndex = this.echoDelay - 1;
            if (delayedIndex >= 0 && this.processedBuffer[delayedIndex] !== undefined) {
                return this.processedBuffer[delayedIndex] * this.echoDecay;
            }
            return 0;
        }

        updateEchoEstimate(sample) {
            // Shift buffer and add new sample
            for (let i = this.echoDelay - 1; i > 0; i--) {
                this.processedBuffer[i] = this.processedBuffer[i - 1];
            }
            this.processedBuffer[0] = sample;
        }
    }

    class AdaptiveFilter {
        constructor(filterLength = 64, learningRate = 0.01) {
            this.filterLength = filterLength;
            this.learningRate = learningRate;
            this.weights = new Float32Array(filterLength);
            this.buffer = new Float32Array(filterLength);
            this.bufferIndex = 0;
        }

        process(input) {
            // Add to circular buffer
            this.buffer[this.bufferIndex] = input;
            this.bufferIndex = (this.bufferIndex + 1) % this.filterLength;

            // Calculate filtered output
            let output = 0;
            for (let i = 0; i < this.filterLength; i++) {
                const bufferIndex = (this.bufferIndex + i) % this.filterLength;
                output += this.buffer[bufferIndex] * this.weights[i];
            }

            return output;
        }

        updateWeights(error) {
            // LMS (Least Mean Squares) algorithm
            for (let i = 0; i < this.filterLength; i++) {
                const bufferIndex = (this.bufferIndex + i) % this.filterLength;
                this.weights[i] += this.learningRate * error * this.buffer[bufferIndex];
            }
        }
    }

    class EnhancedNoiseSuppression {
        constructor(audioContext) {
            this.audioContext = audioContext;
            this.noiseProfile = null;
            this.spectralSubtraction = new SpectralSubtraction();
            this.wienerFilter = new WienerFilter();
            this.voiceActivityDetector = new VoiceActivityDetector();
        }

        async initialize() {
            // Initialize noise profile with silence
            await this.estimateNoiseFloor();
        }

        async estimateNoiseFloor() {
            // Collect noise samples during silence
            const noiseSamples = new Float32Array(4096);
            let sampleCount = 0;

            // This would be called during initialization when user is silent
            // For now, we'll use a default noise profile
            this.noiseProfile = {
                spectralFloor: 0.01,
                spectralCeiling: 0.1,
                voiceThreshold: 0.05
            };
        }

        processAudio(inputBuffer, outputBuffer) {
            const input = inputBuffer.getChannelData(0);
            const output = outputBuffer.getChannelData(0);

            // Perform FFT
            const fftData = this.performFFT(input);
            
            // Apply spectral subtraction
            const suppressedSpectrum = this.spectralSubtraction.process(fftData, this.noiseProfile);
            
            // Apply Wiener filter
            const filteredSpectrum = this.wienerFilter.process(suppressedSpectrum);
            
            // Inverse FFT
            const filteredTime = this.performIFFT(filteredSpectrum);
            
            // Copy to output
            for (let i = 0; i < output.length; i++) {
                output[i] = filteredTime[i];
            }
        }

        performFFT(timeData) {
            // Simplified FFT implementation
            // In production, use a proper FFT library
            const fftSize = timeData.length;
            const real = new Float32Array(fftSize);
            const imag = new Float32Array(fftSize);
            
            // This is a placeholder - real implementation would use Web Audio API's AnalyserNode
            // or a proper FFT library
            
            return { real, imag };
        }

        performIFFT(spectrum) {
            // Simplified IFFT implementation
            const fftSize = spectrum.real.length;
            const timeData = new Float32Array(fftSize);
            
            // Placeholder implementation
            for (let i = 0; i < fftSize; i++) {
                timeData[i] = spectrum.real[i];
            }
            
            return timeData;
        }
    }

    class SpectralSubtraction {
        constructor(overSubtractionFactor = 2.0, spectralFloor = 0.01) {
            this.overSubtractionFactor = overSubtractionFactor;
            this.spectralFloor = spectralFloor;
        }

        process(fftData, noiseProfile) {
            const magnitude = new Float32Array(fftData.real.length);
            
            for (let i = 0; i < magnitude.length; i++) {
                const currentMagnitude = Math.sqrt(
                    fftData.real[i] * fftData.real[i] + 
                    fftData.imag[i] * fftData.imag[i]
                );
                
                // Spectral subtraction
                const noiseMagnitude = noiseProfile.spectralFloor;
                const subtractedMagnitude = currentMagnitude - 
                    (this.overSubtractionFactor * noiseMagnitude);
                
                // Apply spectral floor
                const finalMagnitude = Math.max(subtractedMagnitude, 
                    this.spectralFloor * currentMagnitude);
                
                // Update spectrum
                if (currentMagnitude > 0) {
                    const gain = finalMagnitude / currentMagnitude;
                    fftData.real[i] *= gain;
                    fftData.imag[i] *= gain;
                }
            }
            
            return fftData;
        }
    }

    class WienerFilter {
        constructor() {
            this.noiseSpectrum = null;
            this.signalSpectrum = null;
        }

        process(fftData) {
            if (!this.noiseSpectrum || !this.signalSpectrum) {
                return fftData; // No filtering if no reference
            }

            for (let i = 0; i < fftData.real.length; i++) {
                const signalPower = this.signalSpectrum[i];
                const noisePower = this.noiseSpectrum[i];
                
                // Wiener filter gain
                const gain = Math.max(0, (signalPower - noisePower) / signalPower);
                
                fftData.real[i] *= gain;
                fftData.imag[i] *= gain;
            }
            
            return fftData;
        }

        updateNoiseSpectrum(noiseSpectrum) {
            this.noiseSpectrum = noiseSpectrum;
        }

        updateSignalSpectrum(signalSpectrum) {
            this.signalSpectrum = signalSpectrum;
        }
    }

    class AdaptiveBitrateController {
        constructor() {
            this.currentBitrate = 64000; // 64 kbps default
            this.targetLatency = 100; // 100ms target
            this.networkMetrics = new NetworkMetrics();
            this.bitrateLevels = [
                { bitrate: 32000, quality: 'poor' },
                { bitrate: 48000, quality: 'fair' },
                { bitrate: 64000, quality: 'good' },
                { bitrate: 96000, quality: 'excellent' },
                { bitrate: 128000, quality: 'premium' }
            ];
        }

        updateMetrics(latency, packetLoss, jitter) {
            this.networkMetrics.update(latency, packetLoss, jitter);
            this.adaptBitrate();
        }

        adaptBitrate() {
            const networkQuality = this.assessNetworkQuality();
            const targetBitrate = this.selectOptimalBitrate(networkQuality);
            
            if (targetBitrate !== this.currentBitrate) {
                this.currentBitrate = targetBitrate;
                this.notifyBitrateChange(targetBitrate);
            }
        }

        assessNetworkQuality() {
            const metrics = this.networkMetrics;
            
            if (metrics.latency < 50 && metrics.packetLoss < 0.01 && metrics.jitter < 10) {
                return 'excellent';
            } else if (metrics.latency < 100 && metrics.packetLoss < 0.02 && metrics.jitter < 20) {
                return 'good';
            } else if (metrics.latency < 200 && metrics.packetLoss < 0.05 && metrics.jitter < 30) {
                return 'fair';
            } else {
                return 'poor';
            }
        }

        selectOptimalBitrate(networkQuality) {
            switch (networkQuality) {
                case 'excellent':
                    return 128000;
                case 'good':
                    return 64000;
                case 'fair':
                    return 48000;
                case 'poor':
                    return 32000;
                default:
                    return 64000;
            }
        }

        notifyBitrateChange(newBitrate) {
            if (window.enhancedVoiceChat && window.enhancedVoiceChat.onBitrateChange) {
                window.enhancedVoiceChat.onBitrateChange(newBitrate);
            }
            
            console.log(`[AdaptiveBitrate] Bitrate adjusted to ${newBitrate} bps`);
        }
    }

    class NetworkMetrics {
        constructor() {
            this.latency = 0;
            this.packetLoss = 0;
            this.jitter = 0;
            this.samples = [];
            this.maxSamples = 100;
        }

        update(latency, packetLoss, jitter) {
            this.samples.push({
                timestamp: Date.now(),
                latency,
                packetLoss,
                jitter
            });

            // Keep only recent samples
            if (this.samples.length > this.maxSamples) {
                this.samples.shift();
            }

            this.calculateAverages();
        }

        calculateAverages() {
            if (this.samples.length === 0) return;

            const recentSamples = this.samples.slice(-20); // Last 20 samples
            
            this.latency = recentSamples.reduce((sum, s) => sum + s.latency, 0) / recentSamples.length;
            this.packetLoss = recentSamples.reduce((sum, s) => sum + s.packetLoss, 0) / recentSamples.length;
            this.jitter = recentSamples.reduce((sum, s) => sum + s.jitter, 0) / recentSamples.length;
        }
    }

    class JitterBuffer {
        constructor(targetLatency = 100, maxBufferSize = 200) {
            this.targetLatency = targetLatency;
            this.maxBufferSize = maxBufferSize;
            this.buffers = new Map();
            this.sequenceNumbers = new Map();
            this.lastSequenceNumbers = new Map();
        }

        addPacket(participantId, sequenceNumber, audioData) {
            if (!this.buffers.has(participantId)) {
                this.buffers.set(participantId, []);
                this.sequenceNumbers.set(participantId, []);
                this.lastSequenceNumbers.set(participantId, -1);
            }

            const buffer = this.buffers.get(participantId);
            const seqBuffer = this.sequenceNumbers.get(participantId);
            const lastSeq = this.lastSequenceNumbers.get(participantId);

            // Check for duplicate or out-of-order packets
            if (sequenceNumber <= lastSeq) {
                return false; // Duplicate or old packet
            }

            // Insert packet in correct order
            let insertIndex = buffer.length;
            for (let i = 0; i < seqBuffer.length; i++) {
                if (seqBuffer[i] > sequenceNumber) {
                    insertIndex = i;
                    break;
                }
            }

            buffer.splice(insertIndex, 0, audioData);
            seqBuffer.splice(insertIndex, 0, sequenceNumber);

            // Limit buffer size
            if (buffer.length > this.maxBufferSize) {
                buffer.shift();
                seqBuffer.shift();
            }

            this.lastSequenceNumbers.set(participantId, sequenceNumber);
            return true;
        }

        getAudioData(participantId) {
            if (!this.buffers.has(participantId)) {
                return null;
            }

            const buffer = this.buffers.get(participantId);
            const seqBuffer = this.sequenceNumbers.get(participantId);

            if (buffer.length === 0) {
                return null;
            }

            // Calculate how many packets to release based on target latency
            const packetsToRelease = this.calculatePacketsToRelease(participantId);
            
            if (packetsToRelease > 0) {
                const audioData = buffer.slice(0, packetsToRelease);
                buffer.splice(0, packetsToRelease);
                seqBuffer.splice(0, packetsToRelease);
                
                return this.concatenateAudioData(audioData);
            }

            return null;
        }

        calculatePacketsToRelease(participantId) {
            // Simple implementation: release one packet if buffer has enough data
            const buffer = this.buffers.get(participantId);
            
            if (buffer.length >= 2) {
                return 1; // Release one packet
            }
            
            return 0;
        }

        concatenateAudioData(audioDataArray) {
            // Concatenate multiple audio data arrays
            const totalLength = audioDataArray.reduce((sum, data) => sum + data.length, 0);
            const result = new Float32Array(totalLength);
            
            let offset = 0;
            for (const audioData of audioDataArray) {
                result.set(audioData, offset);
                offset += audioData.length;
            }
            
            return result;
        }

        getBufferSize(participantId) {
            const buffer = this.buffers.get(participantId);
            return buffer ? buffer.length : 0;
        }

        getBufferLatency(participantId) {
            const bufferSize = this.getBufferSize(participantId);
            return bufferSize * 20; // Assuming 20ms packets
        }
    }

    // Voice Activity Detector (enhanced version)
    class VoiceActivityDetector {
        constructor(threshold = 0.01) {
            this.threshold = threshold;
            this.isVoiceDetected = false;
            this.confidence = 0;
            this.frameHistory = [];
            this.maxHistory = 10;
        }

        analyzeAudio(audioData) {
            const energy = this.calculateEnergy(audioData);
            const zcr = this.calculateZeroCrossingRate(audioData);
            const spectralCentroid = this.calculateSpectralCentroid(audioData);
            
            // Add to history
            this.frameHistory.push({ energy, zcr, spectralCentroid, timestamp: Date.now() });
            if (this.frameHistory.length > this.maxHistory) {
                this.frameHistory.shift();
            }
            
            // Enhanced detection using multiple features
            this.confidence = this.calculateEnhancedConfidence();
            this.isVoiceDetected = this.confidence > this.threshold;
            
            return {
                detected: this.isVoiceDetected,
                confidence: this.confidence,
                energy: energy,
                zeroCrossingRate: zcr,
                spectralCentroid: spectralCentroid
            };
        }

        calculateEnhancedConfidence() {
            if (this.frameHistory.length < 3) return 0;
            
            const recent = this.frameHistory.slice(-3);
            const avgEnergy = recent.reduce((sum, f) => sum + f.energy, 0) / recent.length;
            const avgZcr = recent.reduce((sum, f) => sum + f.zcr, 0) / recent.length;
            const avgCentroid = recent.reduce((sum, f) => sum + f.spectralCentroid, 0) / recent.length;
            
            // Weighted combination with temporal consistency
            const energyWeight = 0.5;
            const zcrWeight = 0.2;
            const centroidWeight = 0.3;
            
            return (avgEnergy * energyWeight) + 
                   (avgZcr * zcrWeight) + 
                   (avgCentroid * centroidWeight);
        }

        calculateEnergy(audioData) {
            let sum = 0;
            for (let i = 0; i < audioData.length; i++) {
                sum += audioData[i] * audioData[i];
            }
            return Math.sqrt(sum / audioData.length);
        }

        calculateZeroCrossingRate(audioData) {
            let crossings = 0;
            for (let i = 1; i < audioData.length; i++) {
                if ((audioData[i - 1] < 0 && audioData[i] >= 0) ||
                    (audioData[i - 1] >= 0 && audioData[i] < 0)) {
                    crossings++;
                }
            }
            return crossings / audioData.length;
        }

        calculateSpectralCentroid(audioData) {
            // Simplified spectral centroid calculation
            let weightedSum = 0;
            let magnitudeSum = 0;
            
            for (let i = 0; i < audioData.length; i++) {
                const magnitude = Math.abs(audioData[i]);
                weightedSum += i * magnitude;
                magnitudeSum += magnitude;
            }
            
            return magnitudeSum > 0 ? weightedSum / magnitudeSum : 0;
        }
    }

    // Public API
    window.advancedAudioProcessing = {
        AdvancedEchoCancellation,
        EnhancedNoiseSuppression,
        AdaptiveBitrateController,
        JitterBuffer,
        VoiceActivityDetector,
        NetworkMetrics
    };

    console.log('[AdvancedAudioProcessing] Phase 2 enhancements loaded');
})();
