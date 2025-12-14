/**
 * TouchGestures.js - Gestos Táctiles con Hammer.js
 * Manejo de gestos touch para el editor de Reels
 */

class TouchGestures {
    constructor(reelsCreator) {
        this.reelsCreator = reelsCreator;
        this.hammer = null;
        this.activeElement = null;
        this.initialScale = 1;
        this.initialRotation = 0;
        this.initialPosition = { x: 0, y: 0 };
        this.isEnabled = true;

        // Configuración de gestos
        this.config = {
            minScale: 0.5,
            maxScale: 3,
            swipeThreshold: 50,
            swipeVelocity: 0.3
        };

        this.init();
    }

    init() {
        // Verificar si Hammer.js está disponible
        if (typeof Hammer === 'undefined') {
            console.warn('Hammer.js no está cargado. Los gestos táctiles no estarán disponibles.');
            return;
        }

        this.setupGlobalGestures();
    }

    /**
     * Configura gestos globales para navegación
     */
    setupGlobalGestures() {
        const overlay = document.getElementById('reelsCreatorOverlay');
        if (!overlay) return;

        this.hammer = new Hammer(overlay, {
            recognizers: [
                [Hammer.Swipe, { direction: Hammer.DIRECTION_HORIZONTAL }],
                [Hammer.Tap, { event: 'doubletap', taps: 2 }]
            ]
        });

        // Swipe para navegación entre pantallas
        this.hammer.on('swipeleft swiperight', (e) => {
            if (!this.isEnabled) return;
            this.handleSwipe(e);
        });

        // Doble tap para acciones rápidas
        this.hammer.on('doubletap', (e) => {
            if (!this.isEnabled) return;
            this.handleDoubleTap(e);
        });
    }

    /**
     * Configura gestos para un elemento de capa (texto, sticker)
     */
    setupLayerGestures(element, layerManager) {
        if (typeof Hammer === 'undefined' || !element) return;

        const hammer = new Hammer.Manager(element, {
            recognizers: [
                [Hammer.Pan, { threshold: 0 }],
                [Hammer.Pinch, { enable: true }],
                [Hammer.Rotate, { enable: true }],
                [Hammer.Tap]
            ]
        });

        // Permitir reconocimiento simultáneo de pinch y rotate
        hammer.get('pinch').recognizeWith(hammer.get('rotate'));

        let startScale = 1;
        let startRotation = 0;
        let startX = 0;
        let startY = 0;

        // Tap para seleccionar
        hammer.on('tap', () => {
            if (layerManager) {
                const layerId = element.dataset.layerId;
                if (layerId) {
                    layerManager.selectLayer(layerId);
                }
            }
        });

        // Pan para mover
        hammer.on('panstart', (e) => {
            element.classList.add('dragging');
            startX = parseFloat(element.style.left) || 0;
            startY = parseFloat(element.style.top) || 0;
        });

        hammer.on('panmove', (e) => {
            const container = element.parentElement;
            if (!container) return;

            const containerRect = container.getBoundingClientRect();
            const newX = startX + (e.deltaX / containerRect.width * 100);
            const newY = startY + (e.deltaY / containerRect.height * 100);

            // Limitar dentro del contenedor
            element.style.left = `${Math.max(0, Math.min(100, newX))}%`;
            element.style.top = `${Math.max(0, Math.min(100, newY))}%`;
        });

        hammer.on('panend', () => {
            element.classList.remove('dragging');
        });

        // Pinch para escalar
        hammer.on('pinchstart', () => {
            startScale = parseFloat(element.dataset.scale) || 1;
        });

        hammer.on('pinchmove', (e) => {
            let newScale = startScale * e.scale;
            newScale = Math.max(this.config.minScale, Math.min(this.config.maxScale, newScale));
            element.dataset.scale = newScale;
            this.updateElementTransform(element);
        });

        // Rotate para rotar
        hammer.on('rotatestart', () => {
            startRotation = parseFloat(element.dataset.rotation) || 0;
        });

        hammer.on('rotatemove', (e) => {
            const newRotation = startRotation + e.rotation;
            element.dataset.rotation = newRotation;
            this.updateElementTransform(element);
        });

        // Guardar referencia al hammer en el elemento
        element._hammer = hammer;

        return hammer;
    }

    /**
     * Actualiza la transformación del elemento
     */
    updateElementTransform(element) {
        const scale = parseFloat(element.dataset.scale) || 1;
        const rotation = parseFloat(element.dataset.rotation) || 0;
        element.style.transform = `translate(-50%, -50%) scale(${scale}) rotate(${rotation}deg)`;
    }

    /**
     * Configura gestos para el canvas de dibujo
     */
    setupDrawingGestures(canvas, drawingCanvas) {
        if (typeof Hammer === 'undefined' || !canvas) return;

        const hammer = new Hammer(canvas, {
            recognizers: [
                [Hammer.Pan, { threshold: 0, pointers: 1 }],
                [Hammer.Pinch, { enable: true }]
            ]
        });

        // Un dedo para dibujar (manejado por DrawingCanvas)
        // Dos dedos para hacer zoom

        let initialDistance = 0;

        hammer.on('pinchstart', (e) => {
            initialDistance = e.scale;
            // Deshabilitar dibujo temporalmente
            if (drawingCanvas) {
                drawingCanvas.isDrawing = false;
            }
        });

        hammer.on('pinchmove', (e) => {
            // Zoom en el canvas si es necesario
            // Por ahora solo prevenimos el dibujo durante pinch
        });

        canvas._hammer = hammer;
    }

    /**
     * Maneja gestos de swipe
     */
    handleSwipe(e) {
        const currentScreen = this.reelsCreator?.currentScreen;
        if (!currentScreen) return;

        // Solo permitir swipe en ciertas pantallas
        const swipeableScreens = ['selection', 'preview'];

        if (e.type === 'swiperight') {
            // Swipe derecha = volver
            if (currentScreen === 'preview') {
                this.reelsCreator.retake();
            } else if (currentScreen === 'editor') {
                this.reelsCreator.showScreen('preview');
            } else if (currentScreen === 'publish') {
                this.reelsCreator.showScreen('editor');
            }
        } else if (e.type === 'swipeleft') {
            // Swipe izquierda = avanzar
            if (currentScreen === 'preview') {
                this.reelsCreator.showScreen('editor');
            } else if (currentScreen === 'editor') {
                this.reelsCreator.finishEditing();
            }
        }
    }

    /**
     * Maneja doble tap
     */
    handleDoubleTap(e) {
        const currentScreen = this.reelsCreator?.currentScreen;

        if (currentScreen === 'editor') {
            // Doble tap en editor = toggle play/pause para video
            const video = document.getElementById('editorMedia');
            if (video && video.tagName === 'VIDEO') {
                if (video.paused) {
                    video.play();
                } else {
                    video.pause();
                }
            }
        }
    }

    /**
     * Configura gestos para la barra de filtros
     */
    setupFilterScrollGestures(container) {
        if (typeof Hammer === 'undefined' || !container) return;

        const hammer = new Hammer(container, {
            recognizers: [
                [Hammer.Pan, { direction: Hammer.DIRECTION_HORIZONTAL }]
            ]
        });

        let startScrollLeft = 0;

        hammer.on('panstart', () => {
            startScrollLeft = container.scrollLeft;
        });

        hammer.on('panmove', (e) => {
            container.scrollLeft = startScrollLeft - e.deltaX;
        });

        container._hammer = hammer;
    }

    /**
     * Habilita o deshabilita los gestos
     */
    setEnabled(enabled) {
        this.isEnabled = enabled;
    }

    /**
     * Destruye los gestos de un elemento
     */
    destroyElementGestures(element) {
        if (element._hammer) {
            element._hammer.destroy();
            delete element._hammer;
        }
    }

    /**
     * Destruye todos los gestos
     */
    destroy() {
        if (this.hammer) {
            this.hammer.destroy();
            this.hammer = null;
        }
    }
}

/**
 * TouchFeedback - Retroalimentación visual para toques
 */
class TouchFeedback {
    constructor() {
        this.ripplePool = [];
        this.maxRipples = 5;
        this.init();
    }

    init() {
        // Crear pool de ripples
        for (let i = 0; i < this.maxRipples; i++) {
            const ripple = document.createElement('div');
            ripple.className = 'touch-ripple';
            document.body.appendChild(ripple);
            this.ripplePool.push(ripple);
        }

        // Agregar listeners
        document.addEventListener('touchstart', (e) => this.showRipple(e), { passive: true });
    }

    showRipple(e) {
        const touch = e.touches[0];
        if (!touch) return;

        // Encontrar un ripple disponible
        const ripple = this.ripplePool.find(r => !r.classList.contains('active'));
        if (!ripple) return;

        ripple.style.left = `${touch.clientX}px`;
        ripple.style.top = `${touch.clientY}px`;
        ripple.classList.add('active');

        setTimeout(() => {
            ripple.classList.remove('active');
        }, 500);
    }

    destroy() {
        this.ripplePool.forEach(ripple => ripple.remove());
        this.ripplePool = [];
    }
}

/**
 * OrientationHandler - Manejo de orientación del dispositivo
 */
class OrientationHandler {
    constructor(reelsCreator) {
        this.reelsCreator = reelsCreator;
        this.currentOrientation = this.getOrientation();
        this.listeners = [];

        this.init();
    }

    init() {
        // Escuchar cambios de orientación
        window.addEventListener('orientationchange', () => this.handleOrientationChange());
        window.addEventListener('resize', () => this.handleResize());

        // Configuración inicial
        this.applyOrientationStyles();
    }

    getOrientation() {
        if (window.matchMedia('(orientation: portrait)').matches) {
            return 'portrait';
        }
        return 'landscape';
    }

    handleOrientationChange() {
        setTimeout(() => {
            this.currentOrientation = this.getOrientation();
            this.applyOrientationStyles();
            this.notifyListeners();
        }, 100);
    }

    handleResize() {
        const newOrientation = this.getOrientation();
        if (newOrientation !== this.currentOrientation) {
            this.currentOrientation = newOrientation;
            this.applyOrientationStyles();
            this.notifyListeners();
        }
    }

    applyOrientationStyles() {
        const overlay = document.getElementById('reelsCreatorOverlay');
        if (!overlay) return;

        overlay.classList.remove('orientation-portrait', 'orientation-landscape');
        overlay.classList.add(`orientation-${this.currentOrientation}`);

        // Ajustar layout según orientación
        if (this.currentOrientation === 'landscape') {
            document.documentElement.style.setProperty('--reels-preview-height', '100%');
            document.documentElement.style.setProperty('--reels-controls-position', 'right');
        } else {
            document.documentElement.style.setProperty('--reels-preview-height', '60vh');
            document.documentElement.style.setProperty('--reels-controls-position', 'bottom');
        }
    }

    onOrientationChange(callback) {
        this.listeners.push(callback);
    }

    notifyListeners() {
        this.listeners.forEach(cb => cb(this.currentOrientation));
    }

    destroy() {
        this.listeners = [];
    }
}
