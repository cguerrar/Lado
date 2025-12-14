/**
 * ReelsCreator.js - Orquestador Principal
 * Creador de contenido estilo Instagram Reels/TikTok
 */

class ReelsCreator {
    constructor() {
        this.overlay = null;
        this.currentScreen = 'selection';
        this.capturedMedia = null;
        this.mediaType = null; // 'photo' o 'video'
        this.cameraCapture = null;
        this.fileUpload = null;

        this.init();
    }

    init() {
        this.createOverlay();
        this.bindEvents();
    }

    createOverlay() {
        const overlay = document.createElement('div');
        overlay.id = 'reelsCreatorOverlay';
        overlay.className = 'reels-creator-overlay';
        overlay.innerHTML = this.getOverlayHTML();
        document.body.appendChild(overlay);
        this.overlay = overlay;

        // Initialize modules
        this.cameraCapture = new CameraCapture(this);
        this.fileUpload = new FileUpload(this);
    }

    getOverlayHTML() {
        return `
            <!-- Header -->
            <div class="reels-header">
                <button class="reels-close-btn" id="reelsCloseBtn">&times;</button>
                <span class="reels-title">Crear</span>
                <button class="reels-next-btn" id="reelsNextBtn">Siguiente</button>
            </div>

            <!-- Main Content -->
            <div class="reels-content">
                <!-- Selection Screen -->
                <div class="reels-selection-screen" id="selectionScreen">
                    <h2 class="reels-selection-title">¿Cómo quieres crear tu contenido?</h2>
                    <div class="reels-options">
                        <button class="reels-option-btn" id="openCameraBtn">
                            <div class="reels-option-icon">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"></path>
                                    <circle cx="12" cy="13" r="4"></circle>
                                </svg>
                            </div>
                            <span class="reels-option-text">Grabar</span>
                        </button>
                        <button class="reels-option-btn" id="openUploadBtn">
                            <div class="reels-option-icon">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                                    <polyline points="17 8 12 3 7 8"></polyline>
                                    <line x1="12" y1="3" x2="12" y2="15"></line>
                                </svg>
                            </div>
                            <span class="reels-option-text">Subir Archivo</span>
                        </button>
                    </div>
                </div>

                <!-- Camera Screen -->
                <div class="reels-camera-screen" id="cameraScreen">
                    <div class="camera-preview-container">
                        <video id="cameraPreview" autoplay playsinline muted></video>
                        <div class="recording-timer" id="recordingTimer">00:00</div>
                    </div>
                    <div class="mode-toggle">
                        <button class="active" data-mode="photo">Foto</button>
                        <button data-mode="video">Video</button>
                    </div>
                    <div class="camera-controls">
                        <button class="camera-switch-btn" id="switchCameraBtn" title="Cambiar cámara">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M20 16v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-4"></path>
                                <path d="M4 8V4a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v4"></path>
                                <polyline points="7 11 12 16 17 11"></polyline>
                                <polyline points="7 5 12 10 17 5"></polyline>
                            </svg>
                        </button>
                        <button class="capture-btn" id="captureBtn"></button>
                        <div style="width: 50px;"></div>
                    </div>
                </div>

                <!-- Upload Screen -->
                <div class="reels-upload-screen" id="uploadScreen">
                    <div class="upload-dropzone" id="uploadDropzone">
                        <div class="upload-icon">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                                <polyline points="17 8 12 3 7 8"></polyline>
                                <line x1="12" y1="3" x2="12" y2="15"></line>
                            </svg>
                        </div>
                        <p class="upload-text">Arrastra tu archivo aquí o <span>selecciona</span></p>
                        <p class="upload-hint">Fotos: JPG, PNG, GIF • Videos: MP4, MOV, WebM</p>
                        <input type="file" id="fileInput" accept="image/*,video/*">
                    </div>
                </div>

                <!-- Preview Screen -->
                <div class="reels-preview-screen" id="previewScreen">
                    <div class="preview-container" id="previewContainer">
                        <!-- Media will be inserted here -->
                    </div>
                    <div class="preview-actions">
                        <button class="preview-action-btn preview-retake-btn" id="retakeBtn">Volver</button>
                        <button class="preview-action-btn preview-use-btn" id="useMediaBtn">Usar</button>
                    </div>
                </div>

                <!-- Publish Screen -->
                <div class="reels-publish-screen" id="publishScreen">
                    <div class="publish-preview" id="publishPreview">
                        <!-- Media preview -->
                    </div>
                    <form class="publish-form" id="publishForm">
                        <div class="publish-form-group">
                            <label>Descripción</label>
                            <textarea id="publishCaption" placeholder="Escribe una descripción..." maxlength="2200"></textarea>
                        </div>

                        <div class="publish-form-group">
                            <label>Publicar en</label>
                            <div class="lado-selector" id="ladoSelector">
                                <div class="lado-option selected" data-lado="A">
                                    <div class="lado-option-title">Lado A</div>
                                    <div class="lado-option-desc">Contenido público</div>
                                </div>
                                <div class="lado-option" data-lado="B">
                                    <div class="lado-option-title">Lado B</div>
                                    <div class="lado-option-desc">Contenido premium</div>
                                </div>
                            </div>
                        </div>

                        <div class="publish-toggle" id="gratisToggle" style="display: none;">
                            <span class="publish-toggle-label">Contenido gratuito</span>
                            <label class="toggle-switch">
                                <input type="checkbox" id="esGratis">
                                <span class="toggle-slider"></span>
                            </label>
                        </div>

                        <div class="publish-toggle">
                            <span class="publish-toggle-label">Permitir comentarios</span>
                            <label class="toggle-switch">
                                <input type="checkbox" id="permitirComentarios" checked>
                                <span class="toggle-slider"></span>
                            </label>
                        </div>

                        <button type="submit" class="publish-submit-btn" id="publishBtn">
                            Publicar
                        </button>
                    </form>
                </div>

                <!-- Loading -->
                <div class="reels-loading" id="reelsLoading">
                    <div class="reels-spinner"></div>
                </div>
            </div>
        `;
    }

    bindEvents() {
        // Close button
        document.getElementById('reelsCloseBtn').addEventListener('click', () => this.close());

        // Selection buttons
        document.getElementById('openCameraBtn').addEventListener('click', () => this.showScreen('camera'));
        document.getElementById('openUploadBtn').addEventListener('click', () => this.showScreen('upload'));

        // Retake button
        document.getElementById('retakeBtn').addEventListener('click', () => this.retake());

        // Use media button
        document.getElementById('useMediaBtn').addEventListener('click', () => this.showScreen('publish'));

        // Lado selector
        document.querySelectorAll('.lado-option').forEach(option => {
            option.addEventListener('click', () => this.selectLado(option));
        });

        // Publish form
        document.getElementById('publishForm').addEventListener('submit', (e) => this.handlePublish(e));

        // Keyboard
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.overlay.classList.contains('active')) {
                this.close();
            }
        });
    }

    open() {
        this.overlay.classList.add('active');
        document.body.style.overflow = 'hidden';
        this.showScreen('selection');
    }

    close() {
        this.overlay.classList.remove('active');
        document.body.style.overflow = '';
        this.cameraCapture.stopCamera();
        this.reset();
    }

    reset() {
        this.capturedMedia = null;
        this.mediaType = null;
        this.showScreen('selection');
        document.getElementById('publishCaption').value = '';
        document.getElementById('previewContainer').innerHTML = '';
        document.getElementById('publishPreview').innerHTML = '';
    }

    showScreen(screenName) {
        // Hide all screens
        document.querySelectorAll('.reels-selection-screen, .reels-camera-screen, .reels-upload-screen, .reels-preview-screen, .reels-publish-screen')
            .forEach(screen => screen.classList.remove('active'));

        // Show target screen
        const screenMap = {
            'selection': 'selectionScreen',
            'camera': 'cameraScreen',
            'upload': 'uploadScreen',
            'preview': 'previewScreen',
            'publish': 'publishScreen'
        };

        const targetScreen = document.getElementById(screenMap[screenName]);
        if (targetScreen) {
            targetScreen.classList.add('active');
        }

        // Handle camera
        if (screenName === 'camera') {
            this.cameraCapture.startCamera();
        } else {
            this.cameraCapture.stopCamera();
        }

        // Handle next button visibility
        const nextBtn = document.getElementById('reelsNextBtn');
        nextBtn.classList.toggle('visible', screenName === 'preview');

        // Handle publish preview
        if (screenName === 'publish') {
            this.setupPublishPreview();
        }

        this.currentScreen = screenName;
    }

    setMedia(blob, type) {
        this.capturedMedia = blob;
        this.mediaType = type;
        this.showPreview();
    }

    showPreview() {
        const container = document.getElementById('previewContainer');
        container.innerHTML = '';

        const url = URL.createObjectURL(this.capturedMedia);

        if (this.mediaType === 'photo') {
            const img = document.createElement('img');
            img.id = 'previewMedia';
            img.src = url;
            container.appendChild(img);
        } else {
            const video = document.createElement('video');
            video.id = 'previewMedia';
            video.src = url;
            video.controls = true;
            video.autoplay = true;
            video.loop = true;
            container.appendChild(video);
        }

        this.showScreen('preview');
    }

    setupPublishPreview() {
        const container = document.getElementById('publishPreview');
        container.innerHTML = '';

        if (!this.capturedMedia) return;

        const url = URL.createObjectURL(this.capturedMedia);

        if (this.mediaType === 'photo') {
            const img = document.createElement('img');
            img.src = url;
            container.appendChild(img);
        } else {
            const video = document.createElement('video');
            video.src = url;
            video.autoplay = true;
            video.loop = true;
            video.muted = true;
            container.appendChild(video);
        }
    }

    retake() {
        this.capturedMedia = null;
        this.mediaType = null;
        this.showScreen('selection');
    }

    selectLado(option) {
        document.querySelectorAll('.lado-option').forEach(opt => opt.classList.remove('selected'));
        option.classList.add('selected');

        // Show/hide gratis toggle for Lado B
        const gratisToggle = document.getElementById('gratisToggle');
        gratisToggle.style.display = option.dataset.lado === 'B' ? 'flex' : 'none';
    }

    async handlePublish(e) {
        e.preventDefault();

        if (!this.capturedMedia) {
            this.showMessage('No hay contenido para publicar', 'error');
            return;
        }

        this.showLoading(true);

        try {
            const formData = new FormData();

            // Add the media file
            const extension = this.mediaType === 'photo' ? 'jpg' : 'mp4';
            const filename = `reels_${Date.now()}.${extension}`;
            formData.append('archivo', this.capturedMedia, filename);

            // Add form data
            formData.append('descripcion', document.getElementById('publishCaption').value);

            const selectedLado = document.querySelector('.lado-option.selected');
            formData.append('lado', selectedLado ? selectedLado.dataset.lado : 'A');

            formData.append('esGratis', document.getElementById('esGratis').checked);
            formData.append('permitirComentarios', document.getElementById('permitirComentarios').checked);
            formData.append('tipo', this.mediaType === 'photo' ? 'Imagen' : 'Video');

            const response = await fetch('/Contenido/CrearDesdeReels', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                this.showMessage('Publicado exitosamente!', 'success');
                setTimeout(() => {
                    this.close();
                    if (result.redirectUrl) {
                        window.location.href = result.redirectUrl;
                    }
                }, 1500);
            } else {
                this.showMessage(result.message || 'Error al publicar', 'error');
            }
        } catch (error) {
            console.error('Error publishing:', error);
            this.showMessage('Error al publicar el contenido', 'error');
        } finally {
            this.showLoading(false);
        }
    }

    showLoading(show) {
        document.getElementById('reelsLoading').classList.toggle('active', show);
        document.getElementById('publishBtn').disabled = show;
    }

    showMessage(text, type) {
        const existing = document.querySelector('.reels-message');
        if (existing) existing.remove();

        const msg = document.createElement('div');
        msg.className = `reels-message ${type}`;
        msg.textContent = text;
        document.body.appendChild(msg);

        setTimeout(() => msg.remove(), 3000);
    }
}

// Global instance
let reelsCreator = null;

// Function to open creator
function abrirCreadorReels() {
    if (!reelsCreator) {
        reelsCreator = new ReelsCreator();
    }
    reelsCreator.open();
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    // Pre-initialize for faster first open
    reelsCreator = new ReelsCreator();
});
