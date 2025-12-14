/**
 * ExportManager.js - Gestión de Exportación
 * Exporta fotos y videos con filtros aplicados
 */

class ExportManager {
    constructor(filterEngine) {
        this.filterEngine = filterEngine;
        this.isExporting = false;
        this.progress = 0;
        this.onProgress = null;
    }

    /**
     * Exporta una imagen con filtro aplicado
     */
    async exportImage(source, options = {}) {
        const {
            quality = 0.92,
            format = 'image/jpeg',
            maxWidth = 1920,
            maxHeight = 1920
        } = options;

        return new Promise((resolve, reject) => {
            try {
                let img;

                if (source instanceof HTMLImageElement) {
                    img = source;
                    this.processImage(img, quality, format, maxWidth, maxHeight, resolve);
                } else if (source instanceof Blob) {
                    img = new Image();
                    img.onload = () => {
                        URL.revokeObjectURL(img.src);
                        this.processImage(img, quality, format, maxWidth, maxHeight, resolve);
                    };
                    img.onerror = () => reject(new Error('Error loading image'));
                    img.src = URL.createObjectURL(source);
                } else {
                    reject(new Error('Invalid source type'));
                }
            } catch (error) {
                reject(error);
            }
        });
    }

    /**
     * Procesa y exporta la imagen
     */
    processImage(img, quality, format, maxWidth, maxHeight, resolve) {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        // Calcular dimensiones manteniendo aspecto
        let width = img.naturalWidth || img.width;
        let height = img.naturalHeight || img.height;

        if (width > maxWidth) {
            height = (height * maxWidth) / width;
            width = maxWidth;
        }
        if (height > maxHeight) {
            width = (width * maxHeight) / height;
            height = maxHeight;
        }

        canvas.width = width;
        canvas.height = height;

        // Aplicar filtro
        const filterCSS = this.filterEngine.getCurrentFilterCSS();
        ctx.filter = filterCSS === 'none' ? 'none' : filterCSS;

        // Dibujar imagen
        ctx.drawImage(img, 0, 0, width, height);

        // Convertir a blob
        canvas.toBlob((blob) => {
            resolve(blob);
        }, format, quality);
    }

    /**
     * Exporta un video con filtro y recorte aplicados
     * Usa MediaRecorder con canvas para aplicar filtros
     */
    async exportVideo(video, trimData, options = {}) {
        const {
            fps = 30,
            videoBitrate = 2500000,
            audioBitrate = 128000
        } = options;

        if (this.isExporting) {
            throw new Error('Export already in progress');
        }

        this.isExporting = true;
        this.progress = 0;

        try {
            const result = await this.processVideoExport(video, trimData, fps, videoBitrate);
            this.isExporting = false;
            return result;
        } catch (error) {
            this.isExporting = false;
            throw error;
        }
    }

    /**
     * Procesa la exportación del video frame por frame
     */
    async processVideoExport(video, trimData, fps, videoBitrate) {
        return new Promise(async (resolve, reject) => {
            try {
                const { start, end, duration } = trimData;
                const totalFrames = Math.ceil(duration * fps);

                // Crear canvas para renderizar frames
                const canvas = document.createElement('canvas');
                canvas.width = video.videoWidth;
                canvas.height = video.videoHeight;
                const ctx = canvas.getContext('2d');

                // Stream del canvas
                const stream = canvas.captureStream(fps);

                // Intentar capturar audio del video original
                let audioTrack = null;
                try {
                    if (video.captureStream) {
                        const videoStream = video.captureStream();
                        const audioTracks = videoStream.getAudioTracks();
                        if (audioTracks.length > 0) {
                            audioTrack = audioTracks[0];
                            stream.addTrack(audioTrack);
                        }
                    }
                } catch (e) {
                    console.warn('Could not capture audio track:', e);
                }

                // Configurar MediaRecorder
                const mimeType = this.getSupportedMimeType();
                const mediaRecorder = new MediaRecorder(stream, {
                    mimeType: mimeType,
                    videoBitsPerSecond: videoBitrate
                });

                const chunks = [];
                mediaRecorder.ondataavailable = (e) => {
                    if (e.data.size > 0) {
                        chunks.push(e.data);
                    }
                };

                mediaRecorder.onstop = () => {
                    const blob = new Blob(chunks, { type: mimeType });
                    resolve(blob);
                };

                mediaRecorder.onerror = (e) => {
                    reject(new Error('MediaRecorder error: ' + e.error));
                };

                // Iniciar grabación
                mediaRecorder.start(100);

                // Renderizar frames
                video.currentTime = start;
                video.muted = true;

                const filterCSS = this.filterEngine.getCurrentFilterCSS();
                const frameInterval = 1000 / fps;
                let currentFrame = 0;

                const renderFrame = () => {
                    if (!this.isExporting) {
                        mediaRecorder.stop();
                        return;
                    }

                    const currentTime = video.currentTime;

                    if (currentTime >= end || currentFrame >= totalFrames) {
                        // Terminamos
                        setTimeout(() => {
                            mediaRecorder.stop();
                        }, 100);
                        return;
                    }

                    // Aplicar filtro y dibujar frame
                    ctx.filter = filterCSS === 'none' ? 'none' : filterCSS;
                    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

                    // Actualizar progreso
                    currentFrame++;
                    this.progress = (currentFrame / totalFrames) * 100;
                    if (this.onProgress) {
                        this.onProgress(this.progress);
                    }

                    // Siguiente frame
                    video.currentTime = start + (currentFrame / fps);

                    video.addEventListener('seeked', function onSeeked() {
                        video.removeEventListener('seeked', onSeeked);
                        setTimeout(renderFrame, frameInterval / 2);
                    }, { once: true });
                };

                // Esperar a que el video esté listo en la posición inicial
                video.addEventListener('seeked', function onFirstSeek() {
                    video.removeEventListener('seeked', onFirstSeek);
                    renderFrame();
                }, { once: true });

            } catch (error) {
                reject(error);
            }
        });
    }

    /**
     * Exportación simplificada para videos (sin re-encoding)
     * Usa el video original y solo aplica el trim en el servidor
     */
    async exportVideoSimple(originalBlob, trimData) {
        // Para MVP, retornamos el blob original con metadata de trim
        // El recorte real se puede hacer en el servidor con FFmpeg
        return {
            blob: originalBlob,
            trimStart: trimData.start,
            trimEnd: trimData.end,
            filterApplied: this.filterEngine.currentFilter
        };
    }

    /**
     * Exporta un frame del video como imagen
     */
    async exportVideoFrame(video, time = null) {
        return new Promise((resolve, reject) => {
            try {
                const canvas = document.createElement('canvas');
                canvas.width = video.videoWidth;
                canvas.height = video.videoHeight;
                const ctx = canvas.getContext('2d');

                const captureFrame = () => {
                    const filterCSS = this.filterEngine.getCurrentFilterCSS();
                    ctx.filter = filterCSS === 'none' ? 'none' : filterCSS;
                    ctx.drawImage(video, 0, 0);

                    canvas.toBlob((blob) => {
                        resolve(blob);
                    }, 'image/jpeg', 0.92);
                };

                if (time !== null && video.currentTime !== time) {
                    video.currentTime = time;
                    video.addEventListener('seeked', captureFrame, { once: true });
                } else {
                    captureFrame();
                }
            } catch (error) {
                reject(error);
            }
        });
    }

    /**
     * Obtiene el tipo MIME soportado
     */
    getSupportedMimeType() {
        const types = [
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

    /**
     * Cancela la exportación en curso
     */
    cancelExport() {
        this.isExporting = false;
    }

    /**
     * Obtiene el progreso actual
     */
    getProgress() {
        return this.progress;
    }

    /**
     * Establece callback de progreso
     */
    setProgressCallback(callback) {
        this.onProgress = callback;
    }
}
