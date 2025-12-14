/**
 * ReelsCreator.js - Orquestador Principal
 * Creador de contenido estilo Instagram Reels/TikTok
 */

class ReelsCreator {
    constructor() {
        this.overlay = null;
        this.currentScreen = 'selection';
        this.capturedMedia = null;
        this.originalMedia = null; // Guardamos original para edición
        this.mediaType = null; // 'photo' o 'video'
        this.cameraCapture = null;
        this.fileUpload = null;
        this.filterEngine = null;
        this.videoEditor = null;
        this.exportManager = null;
        // Fase 3: Capas
        this.layerManager = null;
        this.textLayer = null;
        this.stickerLayer = null;
        // Fase 4: Dibujo
        this.drawingCanvas = null;
        // Fase 5: Audio
        this.audioMixer = null;
        // Fase 6: Mobile
        this.touchGestures = null;
        this.mobileOptimizer = null;
        this.orientationHandler = null;

        // Tracking de Object URLs para evitar memory leaks
        this.objectURLs = [];

        this.init();
    }

    init() {
        this.createOverlay();
        this.bindEvents();
        this.initMobileSupport();
        this.configureLadoOptions();
    }

    /**
     * Configura las opciones de Lado según permisos del usuario
     */
    configureLadoOptions() {
        const config = window.reelsCreatorConfig || {};
        const esCreadorVerificado = config.esCreadorVerificado === true;

        // Si no es creador verificado, ocultar opción de Lado B
        if (!esCreadorVerificado) {
            const ladoBOption = document.querySelector('.lado-option[data-lado="B"]');
            if (ladoBOption) {
                ladoBOption.style.display = 'none';
            }
            // Asegurar que Lado A esté seleccionado
            const ladoAOption = document.querySelector('.lado-option[data-lado="A"]');
            if (ladoAOption) {
                ladoAOption.classList.add('selected');
            }
        }
    }

    /**
     * Inicializa soporte móvil
     */
    initMobileSupport() {
        // Inicializar optimizador móvil
        if (typeof MobileOptimizer !== 'undefined') {
            this.mobileOptimizer = getMobileOptimizer();

            // Manejar advertencias de memoria
            window.addEventListener('memorywarning', () => {
                this.handleMemoryWarning();
            });

            // Manejar FPS bajo
            window.addEventListener('lowfps', (e) => {
                console.warn('FPS bajo:', e.detail.fps);
            });
        }

        // Inicializar gestos táctiles
        if (typeof TouchGestures !== 'undefined') {
            this.touchGestures = new TouchGestures(this);
        }

        // Inicializar manejador de orientación
        if (typeof OrientationHandler !== 'undefined') {
            this.orientationHandler = new OrientationHandler(this);
            this.orientationHandler.onOrientationChange((orientation) => {
                this.handleOrientationChange(orientation);
            });
        }

        // Agregar clase de dispositivo móvil si aplica
        if (this.mobileOptimizer?.isMobile) {
            this.overlay.classList.add('is-mobile');
        }
        if (this.mobileOptimizer?.isIOS) {
            this.overlay.classList.add('is-ios');
        }
        if (this.mobileOptimizer?.isAndroid) {
            this.overlay.classList.add('is-android');
        }
    }

    /**
     * Maneja advertencia de memoria baja
     */
    handleMemoryWarning() {
        // Limpiar recursos no esenciales
        if (this.videoEditor) {
            this.videoEditor.clearThumbnails?.();
        }

        // Reducir calidad de preview si es necesario
        console.log('Liberando memoria...');
    }

    /**
     * Maneja cambios de orientación
     */
    handleOrientationChange(orientation) {
        // Ajustar UI según orientación
        const editorContainer = document.getElementById('editorMediaContainer');
        if (editorContainer) {
            editorContainer.classList.remove('landscape', 'portrait');
            editorContainer.classList.add(orientation);
        }

        // Redimensionar canvas de dibujo si está activo
        if (this.drawingCanvas) {
            const container = document.getElementById('editorMediaContainer');
            if (container) {
                const rect = container.getBoundingClientRect();
                this.drawingCanvas.resizeCanvas(rect.width, rect.height);
            }
        }
    }

    createOverlay() {
        const overlay = document.createElement('div');
        overlay.id = 'reelsCreatorOverlay';
        overlay.className = 'reels-creator-overlay';
        overlay.innerHTML = this.getOverlayHTML();
        document.body.appendChild(overlay);
        this.overlay = overlay;

        // Initialize modules
        this.filterEngine = new FilterEngine();
        this.exportManager = new ExportManager(this.filterEngine);
        this.cameraCapture = new CameraCapture(this);
        this.fileUpload = new FileUpload(this);
        this.videoEditor = new VideoEditor(this);
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

                <!-- Preview Screen (quick preview before editor) -->
                <div class="reels-preview-screen" id="previewScreen">
                    <div class="preview-container" id="previewContainer">
                        <!-- Media will be inserted here -->
                    </div>
                    <div class="preview-actions">
                        <button class="preview-action-btn preview-retake-btn" id="retakeBtn">Volver</button>
                        <button class="preview-action-btn preview-use-btn" id="useMediaBtn">Editar</button>
                    </div>
                </div>

                <!-- Editor Screen -->
                <div class="reels-editor-screen" id="editorScreen">
                    <!-- Editor Preview -->
                    <div class="editor-preview-area">
                        <div class="editor-media-container" id="editorMediaContainer">
                            <!-- Media for editing -->
                        </div>

                        <!-- Editor Toolbar -->
                        <div class="editor-toolbar" id="editorToolbar">
                            <button class="toolbar-btn" id="textToolBtn" title="Agregar Texto">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M4 7V4h16v3"></path>
                                    <path d="M9 20h6"></path>
                                    <path d="M12 4v16"></path>
                                </svg>
                                <span>Texto</span>
                            </button>
                            <button class="toolbar-btn" id="stickerToolBtn" title="Agregar Sticker">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <circle cx="12" cy="12" r="10"></circle>
                                    <path d="M8 14s1.5 2 4 2 4-2 4-2"></path>
                                    <line x1="9" y1="9" x2="9.01" y2="9"></line>
                                    <line x1="15" y1="9" x2="15.01" y2="9"></line>
                                </svg>
                                <span>Stickers</span>
                            </button>
                            <button class="toolbar-btn" id="drawToolBtn" title="Dibujar">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M12 19l7-7 3 3-7 7-3-3z"></path>
                                    <path d="M18 13l-1.5-7.5L2 2l3.5 14.5L13 18l5-5z"></path>
                                    <path d="M2 2l7.586 7.586"></path>
                                    <circle cx="11" cy="11" r="2"></circle>
                                </svg>
                                <span>Dibujar</span>
                            </button>
                            <button class="toolbar-btn" id="musicToolBtn" title="Agregar Música">
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M9 18V5l12-2v13"></path>
                                    <circle cx="6" cy="18" r="3"></circle>
                                    <circle cx="18" cy="16" r="3"></circle>
                                </svg>
                                <span>Música</span>
                            </button>
                        </div>

                        <!-- Panels Container -->
                        <div class="editor-panels-container" id="editorPanelsContainer">
                            <!-- Text and Sticker panels will be inserted here -->
                        </div>
                    </div>

                    <!-- Video Timeline (solo para videos) -->
                    <div class="editor-timeline-section" id="timelineSection" style="display: none;">
                        <div class="timeline-header">
                            <button class="editor-play-btn" id="editorPlayBtn">
                                <svg viewBox="0 0 24 24" fill="currentColor"><polygon points="5,3 19,12 5,21"/></svg>
                            </button>
                            <div class="timeline-times">
                                <span id="trimStartTime">0:00</span>
                                <span class="timeline-duration" id="trimDuration">0:00</span>
                                <span id="trimEndTime">0:00</span>
                            </div>
                        </div>
                        <div class="editor-timeline" id="editorTimeline">
                            <div class="timeline-thumbnails" id="timelineThumbnails"></div>
                            <div class="trim-region" id="trimRegion"></div>
                            <div class="trim-handle trim-start" id="trimStartHandle">
                                <div class="handle-bar"></div>
                            </div>
                            <div class="trim-handle trim-end" id="trimEndHandle">
                                <div class="handle-bar"></div>
                            </div>
                            <div class="timeline-playhead" id="timelinePlayhead"></div>
                        </div>
                    </div>

                    <!-- Filter Selection -->
                    <div class="editor-filters-section">
                        <h4 class="editor-section-title">Filtros</h4>
                        <div class="filters-scroll" id="filtersScroll">
                            <!-- Filters will be generated here -->
                        </div>
                    </div>

                    <!-- Editor Actions -->
                    <div class="editor-actions">
                        <button class="editor-action-btn editor-back-btn" id="editorBackBtn">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <polyline points="15 18 9 12 15 6"></polyline>
                            </svg>
                            Volver
                        </button>
                        <button class="editor-action-btn editor-done-btn" id="editorDoneBtn">
                            Siguiente
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <polyline points="9 18 15 12 9 6"></polyline>
                            </svg>
                        </button>
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
                    <p class="loading-text" id="loadingText">Cargando...</p>
                </div>
            </div>
        `;
    }

    bindEvents() {
        // Close button
        document.getElementById('reelsCloseBtn').addEventListener('click', () => this.close());

        // Next button (header) - goes to editor from preview
        document.getElementById('reelsNextBtn').addEventListener('click', () => {
            if (this.currentScreen === 'preview') {
                this.showScreen('editor');
            }
        });

        // Selection buttons
        document.getElementById('openCameraBtn').addEventListener('click', () => this.showScreen('camera'));
        document.getElementById('openUploadBtn').addEventListener('click', () => this.showScreen('upload'));

        // Retake button
        document.getElementById('retakeBtn').addEventListener('click', () => this.retake());

        // Use media button - now goes to editor
        document.getElementById('useMediaBtn').addEventListener('click', () => this.showScreen('editor'));

        // Editor buttons
        document.getElementById('editorBackBtn').addEventListener('click', () => this.showScreen('preview'));
        document.getElementById('editorDoneBtn').addEventListener('click', () => this.finishEditing());

        // Text, Sticker, Drawing and Music tools
        document.getElementById('textToolBtn').addEventListener('click', () => this.toggleTextPanel());
        document.getElementById('stickerToolBtn').addEventListener('click', () => this.toggleStickerPanel());
        document.getElementById('drawToolBtn').addEventListener('click', () => this.toggleDrawingPanel());
        document.getElementById('musicToolBtn').addEventListener('click', () => this.toggleAudioPanel());

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
            // Delete key to remove selected layer
            if (e.key === 'Delete' && this.layerManager?.selectedLayer) {
                this.layerManager.deleteSelectedLayer();
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
        this.videoEditor.reset();
        this.reset();
    }

    /**
     * Crea un Object URL y lo trackea para limpieza posterior
     */
    createTrackedObjectURL(blob) {
        const url = URL.createObjectURL(blob);
        this.objectURLs.push(url);
        return url;
    }

    /**
     * Revoca todos los Object URLs trackeados para liberar memoria
     */
    revokeAllObjectURLs() {
        this.objectURLs.forEach(url => {
            URL.revokeObjectURL(url);
        });
        this.objectURLs = [];
    }

    reset() {
        // Liberar Object URLs para evitar memory leaks
        this.revokeAllObjectURLs();

        this.capturedMedia = null;
        this.originalMedia = null;
        this.mediaType = null;
        this.filterEngine.reset();

        // Reset layer system
        if (this.layerManager) {
            this.layerManager.clear();
        }
        if (this.textLayer) {
            this.textLayer.hide();
        }
        if (this.stickerLayer) {
            this.stickerLayer.hide();
        }
        // Reset drawing canvas
        if (this.drawingCanvas) {
            this.drawingCanvas.reset();
            this.drawingCanvas.hide();
        }
        // Reset audio mixer
        if (this.audioMixer) {
            this.audioMixer.reset();
            this.audioMixer.hide();
        }

        this.showScreen('selection');
        document.getElementById('publishCaption').value = '';
        document.getElementById('previewContainer').innerHTML = '';
        document.getElementById('publishPreview').innerHTML = '';
        document.getElementById('editorMediaContainer').innerHTML = '';
    }

    showScreen(screenName) {
        // Hide all screens
        document.querySelectorAll('.reels-selection-screen, .reels-camera-screen, .reels-upload-screen, .reels-preview-screen, .reels-editor-screen, .reels-publish-screen')
            .forEach(screen => screen.classList.remove('active'));

        // Show target screen
        const screenMap = {
            'selection': 'selectionScreen',
            'camera': 'cameraScreen',
            'upload': 'uploadScreen',
            'preview': 'previewScreen',
            'editor': 'editorScreen',
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

        // Handle editor setup
        if (screenName === 'editor') {
            this.setupEditor();
        }

        // Handle publish preview
        if (screenName === 'publish') {
            this.setupPublishPreview();
        }

        this.currentScreen = screenName;
    }

    setMedia(blob, type) {
        this.capturedMedia = blob;
        this.originalMedia = blob; // Guardar original
        this.mediaType = type;
        this.showPreview();
    }

    showPreview() {
        const container = document.getElementById('previewContainer');
        container.innerHTML = '';

        const url = this.createTrackedObjectURL(this.capturedMedia);

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

    /**
     * Configura el editor con el contenido capturado
     */
    async setupEditor() {
        const container = document.getElementById('editorMediaContainer');
        container.innerHTML = '';

        // Limpiar capas anteriores
        if (this.layerManager) {
            this.layerManager.destroy();
        }

        const url = this.createTrackedObjectURL(this.originalMedia);
        const timelineSection = document.getElementById('timelineSection');

        if (this.mediaType === 'photo') {
            const img = document.createElement('img');
            img.id = 'editorMedia';
            img.src = url;
            img.className = 'editor-media';
            container.appendChild(img);

            timelineSection.style.display = 'none';
        } else {
            const video = document.createElement('video');
            video.id = 'editorMedia';
            video.src = url;
            video.className = 'editor-media';
            video.loop = true;
            video.muted = true;
            video.playsInline = true;
            container.appendChild(video);

            timelineSection.style.display = 'block';

            // Inicializar editor de video
            await this.videoEditor.loadVideo(video);
        }

        // Inicializar sistema de capas
        this.layerManager = new LayerManager(container);
        this.textLayer = new TextLayer(this.layerManager);
        this.stickerLayer = new StickerLayer(this.layerManager);

        // Inicializar canvas de dibujo
        if (this.drawingCanvas) {
            this.drawingCanvas.destroy();
        }
        this.drawingCanvas = new DrawingCanvas(container);
        this.drawingCanvas.mount(container);

        // Inicializar mezclador de audio
        if (this.audioMixer) {
            this.audioMixer.destroy();
        }
        this.audioMixer = new AudioMixer();

        // Sincronizar audio con video si es video
        if (this.mediaType === 'video') {
            const videoElement = document.getElementById('editorMedia');
            if (videoElement) {
                this.audioMixer.syncWithVideo(videoElement);
            }
        }

        // Configurar gestos táctiles para el editor
        if (this.touchGestures) {
            // Gestos para scroll de filtros
            const filtersScroll = document.getElementById('filtersScroll');
            if (filtersScroll) {
                this.touchGestures.setupFilterScrollGestures(filtersScroll);
            }

            // Gestos para canvas de dibujo
            if (this.drawingCanvas?.canvas) {
                this.touchGestures.setupDrawingGestures(this.drawingCanvas.canvas, this.drawingCanvas);
            }
        }

        // Generar selectores de filtros
        this.generateFilterSelectors();
    }

    /**
     * Toggle panel de texto
     */
    toggleTextPanel() {
        const panelsContainer = document.getElementById('editorPanelsContainer');
        if (this.stickerLayer) this.stickerLayer.hide();
        if (this.drawingCanvas) this.drawingCanvas.hide();
        if (this.audioMixer) this.audioMixer.hide();
        this.textLayer.toggle(panelsContainer);

        // Toggle button active state
        document.getElementById('textToolBtn').classList.toggle('active', this.textLayer.isVisible);
        document.getElementById('stickerToolBtn').classList.remove('active');
        document.getElementById('drawToolBtn').classList.remove('active');
        document.getElementById('musicToolBtn').classList.remove('active');
    }

    /**
     * Toggle panel de stickers
     */
    toggleStickerPanel() {
        const panelsContainer = document.getElementById('editorPanelsContainer');
        if (this.textLayer) this.textLayer.hide();
        if (this.drawingCanvas) this.drawingCanvas.hide();
        if (this.audioMixer) this.audioMixer.hide();
        this.stickerLayer.toggle(panelsContainer);

        // Toggle button active state
        document.getElementById('stickerToolBtn').classList.toggle('active', this.stickerLayer.isVisible);
        document.getElementById('textToolBtn').classList.remove('active');
        document.getElementById('drawToolBtn').classList.remove('active');
        document.getElementById('musicToolBtn').classList.remove('active');
    }

    /**
     * Toggle panel de dibujo
     */
    toggleDrawingPanel() {
        const panelsContainer = document.getElementById('editorPanelsContainer');
        if (this.textLayer) this.textLayer.hide();
        if (this.stickerLayer) this.stickerLayer.hide();
        if (this.audioMixer) this.audioMixer.hide();
        this.drawingCanvas.toggle(panelsContainer);

        // Toggle button active state
        document.getElementById('drawToolBtn').classList.toggle('active', this.drawingCanvas.isVisible);
        document.getElementById('textToolBtn').classList.remove('active');
        document.getElementById('stickerToolBtn').classList.remove('active');
        document.getElementById('musicToolBtn').classList.remove('active');
    }

    /**
     * Toggle panel de audio/música
     */
    toggleAudioPanel() {
        const panelsContainer = document.getElementById('editorPanelsContainer');
        if (this.textLayer) this.textLayer.hide();
        if (this.stickerLayer) this.stickerLayer.hide();
        if (this.drawingCanvas) this.drawingCanvas.hide();
        this.audioMixer.toggle(panelsContainer);

        // Toggle button active state
        document.getElementById('musicToolBtn').classList.toggle('active', this.audioMixer.isVisible);
        document.getElementById('textToolBtn').classList.remove('active');
        document.getElementById('stickerToolBtn').classList.remove('active');
        document.getElementById('drawToolBtn').classList.remove('active');
    }

    /**
     * Genera los botones de filtros
     */
    generateFilterSelectors() {
        const container = document.getElementById('filtersScroll');
        container.innerHTML = '';

        const filters = this.filterEngine.getFilterList();
        const mediaElement = document.getElementById('editorMedia');

        filters.forEach(filter => {
            const filterBtn = document.createElement('button');
            filterBtn.className = 'filter-btn' + (filter.id === 'normal' ? ' active' : '');
            filterBtn.dataset.filter = filter.id;

            // Crear thumbnail con filtro
            const thumbnail = document.createElement('div');
            thumbnail.className = 'filter-thumbnail';
            thumbnail.style.filter = this.filterEngine.filters[filter.id].css;

            // Usar imagen de preview o placeholder
            if (this.mediaType === 'photo' && mediaElement) {
                thumbnail.style.backgroundImage = `url(${mediaElement.src})`;
            } else {
                thumbnail.style.backgroundColor = '#333';
            }

            const label = document.createElement('span');
            label.className = 'filter-label';
            label.textContent = filter.name;

            filterBtn.appendChild(thumbnail);
            filterBtn.appendChild(label);

            filterBtn.addEventListener('click', () => this.applyFilter(filter.id, filterBtn));

            container.appendChild(filterBtn);
        });
    }

    /**
     * Aplica un filtro al contenido
     */
    applyFilter(filterId, buttonElement) {
        // Update active state
        document.querySelectorAll('.filter-btn').forEach(btn => btn.classList.remove('active'));
        buttonElement.classList.add('active');

        // Apply filter
        const mediaElement = document.getElementById('editorMedia');
        this.filterEngine.applyFilter(mediaElement, filterId);
    }

    /**
     * Finaliza la edición y prepara para publicar
     */
    async finishEditing() {
        this.showLoading(true, 'Procesando...');

        try {
            const mediaElement = document.getElementById('editorMedia');
            const hasLayers = this.layerManager && this.layerManager.layers.length > 0;
            const hasDrawing = this.drawingCanvas && this.drawingCanvas.hasDrawing();
            const audioData = this.audioMixer ? this.audioMixer.getAudioData() : null;

            if (this.mediaType === 'photo') {
                // Exportar imagen con filtro, capas y dibujo
                this.capturedMedia = await this.exportImageWithLayers(mediaElement);
                // Guardar metadata de audio para fotos (para slideshow/video generado)
                if (audioData) {
                    this.editMetadata = { audio: audioData };
                }
            } else {
                // Para video, guardamos la metadata de edición
                // El filtro se aplica visualmente, el recorte se puede hacer server-side
                const trimData = this.videoEditor.getTrimData();

                // Si hay filtro aplicado, intentar exportar
                if (this.filterEngine.currentFilter !== 'normal') {
                    // Por ahora usamos exportación simple (el video original con metadata)
                    const exportResult = await this.exportManager.exportVideoSimple(this.originalMedia, trimData);
                    this.capturedMedia = exportResult.blob;
                    // Guardar metadata para posible procesamiento server-side
                    this.editMetadata = {
                        trimStart: trimData.start,
                        trimEnd: trimData.end,
                        filter: this.filterEngine.currentFilter,
                        layers: hasLayers ? this.layerManager.getLayersData() : [],
                        hasDrawing: hasDrawing,
                        audio: audioData
                    };
                } else {
                    this.capturedMedia = this.originalMedia;
                    this.editMetadata = {
                        trimStart: trimData.start,
                        trimEnd: trimData.end,
                        filter: 'normal',
                        layers: hasLayers ? this.layerManager.getLayersData() : [],
                        hasDrawing: hasDrawing,
                        audio: audioData
                    };
                }
            }

            this.showLoading(false);
            this.showScreen('publish');

        } catch (error) {
            console.error('Error finishing edit:', error);
            this.showMessage('Error al procesar el contenido', 'error');
            this.showLoading(false);
        }
    }

    /**
     * Exporta imagen con filtro, capas y dibujo renderizados
     */
    async exportImageWithLayers(imgElement) {
        return new Promise((resolve, reject) => {
            try {
                const canvas = document.createElement('canvas');
                canvas.width = imgElement.naturalWidth || imgElement.width;
                canvas.height = imgElement.naturalHeight || imgElement.height;
                const ctx = canvas.getContext('2d');

                // Aplicar filtro
                const filterCSS = this.filterEngine.getCurrentFilterCSS();
                ctx.filter = filterCSS === 'none' ? 'none' : filterCSS;

                // Dibujar imagen
                ctx.drawImage(imgElement, 0, 0, canvas.width, canvas.height);

                // Resetear filtro para las capas
                ctx.filter = 'none';

                // Renderizar capas (texto y stickers)
                if (this.layerManager && this.layerManager.layers.length > 0) {
                    this.layerManager.renderToCanvas(canvas);
                }

                // Renderizar dibujo
                if (this.drawingCanvas && this.drawingCanvas.hasDrawing()) {
                    this.drawingCanvas.renderToCanvas(canvas);
                }

                // Convertir a blob
                canvas.toBlob((blob) => {
                    resolve(blob);
                }, 'image/jpeg', 0.92);

            } catch (error) {
                reject(error);
            }
        });
    }

    setupPublishPreview() {
        const container = document.getElementById('publishPreview');
        container.innerHTML = '';

        if (!this.capturedMedia) return;

        const url = this.createTrackedObjectURL(this.capturedMedia);

        if (this.mediaType === 'photo') {
            const img = document.createElement('img');
            img.src = url;
            // Aplicar filtro visual en preview
            img.style.filter = this.filterEngine.getCurrentFilterCSS();
            container.appendChild(img);
        } else {
            const video = document.createElement('video');
            video.src = url;
            video.autoplay = true;
            video.loop = true;
            video.muted = true;
            // Aplicar filtro visual en preview
            video.style.filter = this.filterEngine.getCurrentFilterCSS();
            container.appendChild(video);
        }
    }

    retake() {
        this.capturedMedia = null;
        this.originalMedia = null;
        this.mediaType = null;
        this.filterEngine.reset();
        this.videoEditor.reset();
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

        this.showLoading(true, 'Publicando...');

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

            // Add edit metadata if available
            if (this.editMetadata) {
                if (this.editMetadata.trimStart !== undefined) {
                    formData.append('trimStart', this.editMetadata.trimStart);
                }
                if (this.editMetadata.trimEnd !== undefined) {
                    formData.append('trimEnd', this.editMetadata.trimEnd);
                }
                if (this.editMetadata.filter) {
                    formData.append('filter', this.editMetadata.filter);
                }
                // Add audio metadata
                if (this.editMetadata.audio) {
                    formData.append('audioTrackId', this.editMetadata.audio.trackId);
                    formData.append('audioTrackTitle', this.editMetadata.audio.trackTitle);
                    formData.append('audioStartTime', this.editMetadata.audio.startTime);
                    formData.append('audioVolume', this.editMetadata.audio.musicVolume);
                    formData.append('originalVolume', this.editMetadata.audio.originalVolume);
                }
            }

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

    showLoading(show, text = 'Cargando...') {
        document.getElementById('reelsLoading').classList.toggle('active', show);
        document.getElementById('loadingText').textContent = text;
        const publishBtn = document.getElementById('publishBtn');
        if (publishBtn) publishBtn.disabled = show;
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
