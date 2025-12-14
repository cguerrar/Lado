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
        this.allowedImageTypes = [
            'image/jpeg',
            'image/png',
            'image/gif',
            'image/webp',
            'image/heic',      // iPhone HEIC
            'image/heif',      // iPhone HEIF
            'image/avif'       // AVIF moderno
        ];
        this.allowedVideoTypes = [
            'video/mp4',
            'video/webm',
            'video/quicktime',    // MOV
            'video/x-msvideo',    // AVI
            'video/x-m4v',        // M4V
            'video/3gpp',         // 3GP móviles
            'video/x-matroska'    // MKV
        ];

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

        // Determine type - primero por MIME, luego por extensión
        let isImage = this.allowedImageTypes.includes(file.type);

        if (!isImage && !this.allowedVideoTypes.includes(file.type)) {
            // Fallback por extensión
            const ext = file.name.split('.').pop().toLowerCase();
            const imageExtensions = ['jpg', 'jpeg', 'png', 'gif', 'webp', 'heic', 'heif', 'avif'];
            isImage = imageExtensions.includes(ext);
        }

        const type = isImage ? 'photo' : 'video';

        // Set media in reels creator
        this.reels.setMedia(file, type);

        // Reset file input
        document.getElementById('fileInput').value = '';
    }

    validateFile(file) {
        // Validar por MIME type
        let isImage = this.allowedImageTypes.includes(file.type);
        let isVideo = this.allowedVideoTypes.includes(file.type);

        // Fallback: validar por extensión (iOS a veces no reporta MIME type correctamente)
        if (!isImage && !isVideo) {
            const ext = file.name.split('.').pop().toLowerCase();
            const imageExtensions = ['jpg', 'jpeg', 'png', 'gif', 'webp', 'heic', 'heif', 'avif'];
            const videoExtensions = ['mp4', 'webm', 'mov', 'avi', 'm4v', '3gp', 'mkv'];

            isImage = imageExtensions.includes(ext);
            isVideo = videoExtensions.includes(ext);
        }

        // Check type
        if (!isImage && !isVideo) {
            return {
                valid: false,
                message: 'Tipo de archivo no soportado. Usa JPG, PNG, HEIC, GIF, MP4, MOV o WebM.'
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
