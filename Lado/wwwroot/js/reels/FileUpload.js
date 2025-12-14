/**
 * FileUpload.js - Módulo de Subida de Archivos
 * Drag & Drop + Validación
 */

class FileUpload {
    constructor(reelsCreator) {
        this.reels = reelsCreator;

        // Límites de archivo
        this.maxImageSize = 20 * 1024 * 1024; // 20MB
        this.maxVideoSize = 100 * 1024 * 1024; // 100MB
        this.allowedImageTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
        this.allowedVideoTypes = ['video/mp4', 'video/webm', 'video/quicktime', 'video/x-msvideo'];

        this.init();
    }

    init() {
        this.bindEvents();
    }

    bindEvents() {
        const dropzone = document.getElementById('uploadDropzone');
        const fileInput = document.getElementById('fileInput');

        // Click to select
        dropzone.addEventListener('click', () => fileInput.click());

        // File input change
        fileInput.addEventListener('change', (e) => {
            if (e.target.files.length > 0) {
                this.handleFile(e.target.files[0]);
            }
        });

        // Drag events
        dropzone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropzone.classList.add('dragover');
        });

        dropzone.addEventListener('dragleave', (e) => {
            e.preventDefault();
            dropzone.classList.remove('dragover');
        });

        dropzone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropzone.classList.remove('dragover');

            if (e.dataTransfer.files.length > 0) {
                this.handleFile(e.dataTransfer.files[0]);
            }
        });
    }

    handleFile(file) {
        // Validate file
        const validation = this.validateFile(file);

        if (!validation.valid) {
            this.reels.showMessage(validation.message, 'error');
            return;
        }

        // Determine type
        const isImage = this.allowedImageTypes.includes(file.type);
        const type = isImage ? 'photo' : 'video';

        // Set media in reels creator
        this.reels.setMedia(file, type);

        // Reset file input
        document.getElementById('fileInput').value = '';
    }

    validateFile(file) {
        const isImage = this.allowedImageTypes.includes(file.type);
        const isVideo = this.allowedVideoTypes.includes(file.type);

        // Check type
        if (!isImage && !isVideo) {
            return {
                valid: false,
                message: 'Tipo de archivo no soportado. Usa JPG, PNG, GIF, MP4, MOV o WebM.'
            };
        }

        // Check size
        if (isImage && file.size > this.maxImageSize) {
            return {
                valid: false,
                message: `La imagen es muy grande. Máximo ${this.formatSize(this.maxImageSize)}.`
            };
        }

        if (isVideo && file.size > this.maxVideoSize) {
            return {
                valid: false,
                message: `El video es muy grande. Máximo ${this.formatSize(this.maxVideoSize)}.`
            };
        }

        return { valid: true };
    }

    formatSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }
}
