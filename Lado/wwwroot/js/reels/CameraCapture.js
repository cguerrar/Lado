/**
 * CameraCapture.js - Módulo de Captura de Cámara
 * WebRTC + MediaRecorder para fotos y videos
 */

class CameraCapture {
    constructor(reelsCreator) {
        this.reels = reelsCreator;
        this.stream = null;
        this.mediaRecorder = null;
        this.recordedChunks = [];
        this.isRecording = false;
        this.mode = 'photo'; // 'photo' o 'video'
        this.facingMode = 'user'; // 'user' (frontal) o 'environment' (trasera)
        this.recordingStartTime = null;
        this.timerInterval = null;

        this.init();
    }

    init() {
        this.bindEvents();
    }

    bindEvents() {
        // Mode toggle
        document.querySelectorAll('.mode-toggle button').forEach(btn => {
            btn.addEventListener('click', () => this.setMode(btn.dataset.mode));
        });

        // Capture button
        document.getElementById('captureBtn').addEventListener('click', () => this.capture());

        // Switch camera
        document.getElementById('switchCameraBtn').addEventListener('click', () => this.switchCamera());
    }

    async startCamera() {
        try {
            const constraints = {
                video: {
                    facingMode: this.facingMode,
                    width: { ideal: 1080 },
                    height: { ideal: 1920 },
                    aspectRatio: { ideal: 9/16 }
                },
                audio: this.mode === 'video'
            };

            this.stream = await navigator.mediaDevices.getUserMedia(constraints);

            const preview = document.getElementById('cameraPreview');
            preview.srcObject = this.stream;
            preview.play();

        } catch (error) {
            console.error('Error accessing camera:', error);

            if (error.name === 'NotAllowedError') {
                this.reels.showMessage('Permiso de cámara denegado', 'error');
            } else if (error.name === 'NotFoundError') {
                this.reels.showMessage('No se encontró cámara', 'error');
            } else {
                this.reels.showMessage('Error al acceder a la cámara', 'error');
            }

            this.reels.showScreen('selection');
        }
    }

    stopCamera() {
        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }

        const preview = document.getElementById('cameraPreview');
        if (preview) {
            preview.srcObject = null;
        }

        this.stopRecording();
    }

    setMode(mode) {
        this.mode = mode;

        // Update UI
        document.querySelectorAll('.mode-toggle button').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.mode === mode);
        });

        // Restart camera with audio if video mode
        if (this.stream) {
            this.stopCamera();
            this.startCamera();
        }
    }

    async switchCamera() {
        this.facingMode = this.facingMode === 'user' ? 'environment' : 'user';
        await this.stopCamera();
        await this.startCamera();
    }

    capture() {
        if (this.mode === 'photo') {
            this.takePhoto();
        } else {
            if (this.isRecording) {
                this.stopRecording();
            } else {
                this.startRecording();
            }
        }
    }

    takePhoto() {
        const preview = document.getElementById('cameraPreview');
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        // Set canvas size to match video
        canvas.width = preview.videoWidth;
        canvas.height = preview.videoHeight;

        // Mirror the image if using front camera
        if (this.facingMode === 'user') {
            ctx.translate(canvas.width, 0);
            ctx.scale(-1, 1);
        }

        // Draw frame
        ctx.drawImage(preview, 0, 0);

        // Convert to blob
        canvas.toBlob((blob) => {
            this.reels.setMedia(blob, 'photo');
        }, 'image/jpeg', 0.9);
    }

    startRecording() {
        if (!this.stream) return;

        this.recordedChunks = [];

        // Setup MediaRecorder
        const options = {
            mimeType: this.getSupportedMimeType()
        };

        try {
            this.mediaRecorder = new MediaRecorder(this.stream, options);
        } catch (error) {
            console.error('MediaRecorder error:', error);
            this.reels.showMessage('Error al iniciar grabación', 'error');
            return;
        }

        this.mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                this.recordedChunks.push(event.data);
            }
        };

        this.mediaRecorder.onstop = () => {
            const blob = new Blob(this.recordedChunks, {
                type: 'video/webm'
            });
            this.reels.setMedia(blob, 'video');
        };

        this.mediaRecorder.start(100); // Collect data every 100ms
        this.isRecording = true;

        // Update UI
        document.getElementById('captureBtn').classList.add('recording');
        this.startTimer();
    }

    stopRecording() {
        if (this.mediaRecorder && this.isRecording) {
            this.mediaRecorder.stop();
            this.isRecording = false;

            // Update UI
            document.getElementById('captureBtn').classList.remove('recording');
            this.stopTimer();
        }
    }

    startTimer() {
        this.recordingStartTime = Date.now();
        const timerEl = document.getElementById('recordingTimer');
        timerEl.classList.add('active');

        this.timerInterval = setInterval(() => {
            const elapsed = Math.floor((Date.now() - this.recordingStartTime) / 1000);
            const minutes = Math.floor(elapsed / 60).toString().padStart(2, '0');
            const seconds = (elapsed % 60).toString().padStart(2, '0');
            timerEl.textContent = `${minutes}:${seconds}`;

            // Max 60 seconds
            if (elapsed >= 60) {
                this.stopRecording();
            }
        }, 1000);
    }

    stopTimer() {
        if (this.timerInterval) {
            clearInterval(this.timerInterval);
            this.timerInterval = null;
        }

        const timerEl = document.getElementById('recordingTimer');
        timerEl.classList.remove('active');
        timerEl.textContent = '00:00';
    }

    getSupportedMimeType() {
        const types = [
            'video/webm;codecs=vp9,opus',
            'video/webm;codecs=vp8,opus',
            'video/webm;codecs=vp9',
            'video/webm;codecs=vp8',
            'video/webm',
            'video/mp4'
        ];

        for (const type of types) {
            if (MediaRecorder.isTypeSupported(type)) {
                return type;
            }
        }

        return 'video/webm';
    }
}
