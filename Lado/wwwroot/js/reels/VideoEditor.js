/**
 * VideoEditor.js - Editor de Video
 * Timeline interactivo para recorte de videos
 */

class VideoEditor {
    constructor(reelsCreator) {
        this.reels = reelsCreator;
        this.video = null;
        this.duration = 0;
        this.trimStart = 0;
        this.trimEnd = 0;
        this.isPlaying = false;
        this.isDragging = null; // 'start', 'end', 'playhead'
        this.playheadPosition = 0;
        this.thumbnails = [];
        this.thumbnailCount = 10;
        this.updateInterval = null;

        this.init();
    }

    init() {
        this.bindEvents();
    }

    bindEvents() {
        // Play/Pause button
        document.getElementById('editorPlayBtn')?.addEventListener('click', () => this.togglePlay());

        // Timeline drag events
        const timeline = document.getElementById('editorTimeline');
        if (timeline) {
            timeline.addEventListener('mousedown', (e) => this.handleTimelineMouseDown(e));
            timeline.addEventListener('touchstart', (e) => this.handleTimelineTouchStart(e), { passive: false });
        }

        document.addEventListener('mousemove', (e) => this.handleDrag(e));
        document.addEventListener('mouseup', () => this.handleDragEnd());
        document.addEventListener('touchmove', (e) => this.handleTouchDrag(e), { passive: false });
        document.addEventListener('touchend', () => this.handleDragEnd());
    }

    /**
     * Inicializa el editor con un video
     */
    async loadVideo(videoElement) {
        this.video = videoElement;

        // Esperar a que el video esté listo
        await new Promise((resolve) => {
            if (this.video.readyState >= 2) {
                resolve();
            } else {
                this.video.addEventListener('loadeddata', resolve, { once: true });
            }
        });

        this.duration = this.video.duration;
        this.trimStart = 0;
        this.trimEnd = this.duration;
        this.playheadPosition = 0;

        // Generar thumbnails
        await this.generateThumbnails();

        // Actualizar UI
        this.updateUI();

        // Iniciar loop de actualización
        this.startUpdateLoop();
    }

    /**
     * Genera thumbnails del video para el timeline
     */
    async generateThumbnails() {
        this.thumbnails = [];
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        canvas.width = 60;
        canvas.height = 80;

        const interval = this.duration / this.thumbnailCount;

        for (let i = 0; i < this.thumbnailCount; i++) {
            const time = i * interval;
            this.video.currentTime = time;

            await new Promise((resolve) => {
                this.video.addEventListener('seeked', () => {
                    ctx.drawImage(this.video, 0, 0, canvas.width, canvas.height);
                    this.thumbnails.push(canvas.toDataURL('image/jpeg', 0.5));
                    resolve();
                }, { once: true });
            });
        }

        // Volver al inicio
        this.video.currentTime = 0;

        // Renderizar thumbnails
        this.renderThumbnails();
    }

    /**
     * Renderiza los thumbnails en el timeline
     */
    renderThumbnails() {
        const container = document.getElementById('timelineThumbnails');
        if (!container) return;

        container.innerHTML = '';
        this.thumbnails.forEach((thumb, index) => {
            const img = document.createElement('img');
            img.src = thumb;
            img.className = 'timeline-thumbnail';
            img.draggable = false;
            container.appendChild(img);
        });
    }

    /**
     * Actualiza la UI del timeline
     */
    updateUI() {
        // Actualizar handles de trim
        const startHandle = document.getElementById('trimStartHandle');
        const endHandle = document.getElementById('trimEndHandle');
        const trimRegion = document.getElementById('trimRegion');
        const playhead = document.getElementById('timelinePlayhead');

        if (!startHandle || !endHandle || !trimRegion || !playhead) return;

        const startPercent = (this.trimStart / this.duration) * 100;
        const endPercent = (this.trimEnd / this.duration) * 100;
        const playheadPercent = (this.playheadPosition / this.duration) * 100;

        startHandle.style.left = `${startPercent}%`;
        endHandle.style.left = `${endPercent}%`;
        trimRegion.style.left = `${startPercent}%`;
        trimRegion.style.width = `${endPercent - startPercent}%`;
        playhead.style.left = `${playheadPercent}%`;

        // Actualizar tiempos mostrados
        const startTimeEl = document.getElementById('trimStartTime');
        const endTimeEl = document.getElementById('trimEndTime');
        const durationEl = document.getElementById('trimDuration');

        if (startTimeEl) startTimeEl.textContent = this.formatTime(this.trimStart);
        if (endTimeEl) endTimeEl.textContent = this.formatTime(this.trimEnd);
        if (durationEl) durationEl.textContent = this.formatTime(this.trimEnd - this.trimStart);
    }

    /**
     * Formatea tiempo en mm:ss
     */
    formatTime(seconds) {
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }

    /**
     * Maneja mousedown en el timeline
     */
    handleTimelineMouseDown(e) {
        const target = e.target;
        const timeline = document.getElementById('editorTimeline');
        const rect = timeline.getBoundingClientRect();

        if (target.id === 'trimStartHandle' || target.closest('#trimStartHandle')) {
            this.isDragging = 'start';
        } else if (target.id === 'trimEndHandle' || target.closest('#trimEndHandle')) {
            this.isDragging = 'end';
        } else {
            // Click en timeline - mover playhead
            const percent = (e.clientX - rect.left) / rect.width;
            this.setPlayheadPosition(percent * this.duration);
        }

        e.preventDefault();
    }

    /**
     * Maneja touchstart en el timeline
     */
    handleTimelineTouchStart(e) {
        const target = e.target;
        const touch = e.touches[0];
        const timeline = document.getElementById('editorTimeline');
        const rect = timeline.getBoundingClientRect();

        if (target.id === 'trimStartHandle' || target.closest('#trimStartHandle')) {
            this.isDragging = 'start';
        } else if (target.id === 'trimEndHandle' || target.closest('#trimEndHandle')) {
            this.isDragging = 'end';
        } else {
            const percent = (touch.clientX - rect.left) / rect.width;
            this.setPlayheadPosition(percent * this.duration);
        }

        e.preventDefault();
    }

    /**
     * Maneja drag del mouse
     */
    handleDrag(e) {
        if (!this.isDragging) return;

        const timeline = document.getElementById('editorTimeline');
        const rect = timeline.getBoundingClientRect();
        const percent = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
        const time = percent * this.duration;

        this.updateTrimPosition(time);
    }

    /**
     * Maneja drag táctil
     */
    handleTouchDrag(e) {
        if (!this.isDragging) return;

        const touch = e.touches[0];
        const timeline = document.getElementById('editorTimeline');
        const rect = timeline.getBoundingClientRect();
        const percent = Math.max(0, Math.min(1, (touch.clientX - rect.left) / rect.width));
        const time = percent * this.duration;

        this.updateTrimPosition(time);
        e.preventDefault();
    }

    /**
     * Actualiza la posición de trim según el handle que se está arrastrando
     */
    updateTrimPosition(time) {
        const minDuration = 1; // Mínimo 1 segundo

        if (this.isDragging === 'start') {
            this.trimStart = Math.max(0, Math.min(time, this.trimEnd - minDuration));
            if (this.playheadPosition < this.trimStart) {
                this.setPlayheadPosition(this.trimStart);
            }
        } else if (this.isDragging === 'end') {
            this.trimEnd = Math.min(this.duration, Math.max(time, this.trimStart + minDuration));
            if (this.playheadPosition > this.trimEnd) {
                this.setPlayheadPosition(this.trimEnd);
            }
        }

        this.updateUI();
    }

    /**
     * Finaliza el drag
     */
    handleDragEnd() {
        this.isDragging = null;
    }

    /**
     * Establece la posición del playhead
     */
    setPlayheadPosition(time) {
        this.playheadPosition = Math.max(this.trimStart, Math.min(time, this.trimEnd));
        this.video.currentTime = this.playheadPosition;
        this.updateUI();
    }

    /**
     * Toggle play/pause
     */
    togglePlay() {
        if (this.isPlaying) {
            this.pause();
        } else {
            this.play();
        }
    }

    /**
     * Reproduce el video
     */
    play() {
        if (this.playheadPosition >= this.trimEnd) {
            this.video.currentTime = this.trimStart;
            this.playheadPosition = this.trimStart;
        }

        this.video.play();
        this.isPlaying = true;
        this.updatePlayButton();
    }

    /**
     * Pausa el video
     */
    pause() {
        this.video.pause();
        this.isPlaying = false;
        this.updatePlayButton();
    }

    /**
     * Actualiza el icono del botón play
     */
    updatePlayButton() {
        const btn = document.getElementById('editorPlayBtn');
        if (!btn) return;

        if (this.isPlaying) {
            btn.innerHTML = `<svg viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>`;
        } else {
            btn.innerHTML = `<svg viewBox="0 0 24 24" fill="currentColor"><polygon points="5,3 19,12 5,21"/></svg>`;
        }
    }

    /**
     * Inicia el loop de actualización
     */
    startUpdateLoop() {
        this.stopUpdateLoop();

        this.updateInterval = setInterval(() => {
            if (this.isPlaying && this.video) {
                this.playheadPosition = this.video.currentTime;

                // Loop dentro del trim
                if (this.playheadPosition >= this.trimEnd) {
                    this.video.currentTime = this.trimStart;
                    this.playheadPosition = this.trimStart;
                }

                this.updateUI();
            }
        }, 50);
    }

    /**
     * Detiene el loop de actualización
     */
    stopUpdateLoop() {
        if (this.updateInterval) {
            clearInterval(this.updateInterval);
            this.updateInterval = null;
        }
    }

    /**
     * Obtiene los datos de trim actuales
     */
    getTrimData() {
        return {
            start: this.trimStart,
            end: this.trimEnd,
            duration: this.trimEnd - this.trimStart
        };
    }

    /**
     * Resetea el editor
     */
    reset() {
        this.stopUpdateLoop();
        this.pause();
        this.video = null;
        this.duration = 0;
        this.trimStart = 0;
        this.trimEnd = 0;
        this.playheadPosition = 0;
        this.thumbnails = [];
    }

    /**
     * Destructor
     */
    destroy() {
        this.reset();
    }
}
