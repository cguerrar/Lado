/**
 * MobileOptimizer.js - Optimización para dispositivos móviles
 * Manejo de memoria, rendimiento y compatibilidad con iOS Safari
 */

class MobileOptimizer {
    constructor() {
        this.isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent);
        this.isAndroid = /Android/.test(navigator.userAgent);
        this.isMobile = this.isIOS || this.isAndroid || window.innerWidth <= 768;
        this.isSafari = /^((?!chrome|android).)*safari/i.test(navigator.userAgent);

        // Configuración de optimización
        this.config = {
            maxVideoResolution: this.isMobile ? 720 : 1080,
            maxImageSize: this.isMobile ? 2048 : 4096,
            thumbnailQuality: this.isMobile ? 0.6 : 0.8,
            maxHistoryStates: this.isMobile ? 20 : 50,
            debounceDelay: this.isMobile ? 150 : 100
        };

        // Estado de memoria
        this.memoryWarningThreshold = 0.8; // 80%
        this.objectURLs = new Set();

        this.init();
    }

    init() {
        this.applyIOSFixes();
        this.setupMemoryMonitoring();
        this.setupVisibilityHandler();
        this.preventPullToRefresh();
        this.optimizeScrolling();
    }

    /**
     * Aplica correcciones específicas para iOS Safari
     */
    applyIOSFixes() {
        if (!this.isIOS) return;

        // Fix para el viewport en iOS Safari
        const setViewportHeight = () => {
            const vh = window.innerHeight * 0.01;
            document.documentElement.style.setProperty('--vh', `${vh}px`);
        };

        setViewportHeight();
        window.addEventListener('resize', setViewportHeight);
        window.addEventListener('orientationchange', () => {
            setTimeout(setViewportHeight, 100);
        });

        // Fix para el teclado virtual en iOS
        document.addEventListener('focusin', (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') {
                setTimeout(() => {
                    e.target.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }, 300);
            }
        });

        // Fix para el audio en iOS (requiere interacción del usuario)
        document.addEventListener('touchstart', () => {
            // Crear y destruir un audio context para desbloquearlo
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            audioContext.resume().then(() => {
                audioContext.close();
            });
        }, { once: true });

        // Prevenir zoom en inputs
        document.querySelectorAll('input, select, textarea').forEach(el => {
            el.style.fontSize = '16px';
        });
    }

    /**
     * Monitorea el uso de memoria
     */
    setupMemoryMonitoring() {
        if ('memory' in performance) {
            setInterval(() => {
                const memory = performance.memory;
                const usedRatio = memory.usedJSHeapSize / memory.jsHeapSizeLimit;

                if (usedRatio > this.memoryWarningThreshold) {
                    this.handleMemoryWarning();
                }
            }, 10000);
        }
    }

    /**
     * Maneja advertencias de memoria
     */
    handleMemoryWarning() {
        console.warn('Advertencia de memoria alta. Liberando recursos...');

        // Limpiar Object URLs no utilizados
        this.cleanupObjectURLs();

        // Forzar recolección de basura si es posible
        if (window.gc) {
            window.gc();
        }

        // Notificar a la aplicación
        window.dispatchEvent(new CustomEvent('memorywarning'));
    }

    /**
     * Maneja visibilidad de la página
     */
    setupVisibilityHandler() {
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                // Pausar actividades cuando la página no es visible
                this.onPageHidden();
            } else {
                // Reanudar cuando vuelve a ser visible
                this.onPageVisible();
            }
        });
    }

    onPageHidden() {
        // Pausar videos
        document.querySelectorAll('video').forEach(video => {
            if (!video.paused) {
                video.dataset.wasPlaying = 'true';
                video.pause();
            }
        });

        // Pausar audio
        document.querySelectorAll('audio').forEach(audio => {
            if (!audio.paused) {
                audio.dataset.wasPlaying = 'true';
                audio.pause();
            }
        });
    }

    onPageVisible() {
        // Reanudar videos que estaban reproduciéndose
        document.querySelectorAll('video[data-was-playing="true"]').forEach(video => {
            video.play().catch(() => {});
            delete video.dataset.wasPlaying;
        });

        // Reanudar audio
        document.querySelectorAll('audio[data-was-playing="true"]').forEach(audio => {
            audio.play().catch(() => {});
            delete audio.dataset.wasPlaying;
        });
    }

    /**
     * Previene pull-to-refresh en el overlay
     */
    preventPullToRefresh() {
        const overlay = document.getElementById('reelsCreatorOverlay');
        if (!overlay) return;

        let startY = 0;

        overlay.addEventListener('touchstart', (e) => {
            startY = e.touches[0].clientY;
        }, { passive: true });

        overlay.addEventListener('touchmove', (e) => {
            const currentY = e.touches[0].clientY;
            const scrollTop = overlay.scrollTop;

            // Si estamos en la parte superior y deslizando hacia abajo
            if (scrollTop <= 0 && currentY > startY) {
                e.preventDefault();
            }
        }, { passive: false });
    }

    /**
     * Optimiza el scrolling para móviles
     */
    optimizeScrolling() {
        // Usar scroll pasivo donde sea posible
        document.addEventListener('touchstart', () => {}, { passive: true });
        document.addEventListener('touchmove', () => {}, { passive: true });

        // Agregar momentum scrolling a contenedores
        document.querySelectorAll('.track-list, .filters-scroll, .genre-filter').forEach(el => {
            el.style.webkitOverflowScrolling = 'touch';
        });
    }

    /**
     * Optimiza una imagen antes de procesarla
     */
    async optimizeImage(file, maxSize = null) {
        maxSize = maxSize || this.config.maxImageSize;

        return new Promise((resolve, reject) => {
            const reader = new FileReader();

            reader.onload = (e) => {
                const img = new Image();

                img.onload = () => {
                    let width = img.width;
                    let height = img.height;

                    // Redimensionar si excede el tamaño máximo
                    if (width > maxSize || height > maxSize) {
                        const ratio = Math.min(maxSize / width, maxSize / height);
                        width = Math.round(width * ratio);
                        height = Math.round(height * ratio);
                    }

                    const canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;

                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);

                    canvas.toBlob((blob) => {
                        resolve(blob);
                    }, 'image/jpeg', this.config.thumbnailQuality);
                };

                img.onerror = reject;
                img.src = e.target.result;
            };

            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    }

    /**
     * Crea un Object URL y lo registra para limpieza
     */
    createObjectURL(blob) {
        const url = URL.createObjectURL(blob);
        this.objectURLs.add(url);
        return url;
    }

    /**
     * Revoca un Object URL
     */
    revokeObjectURL(url) {
        URL.revokeObjectURL(url);
        this.objectURLs.delete(url);
    }

    /**
     * Limpia todos los Object URLs registrados
     */
    cleanupObjectURLs() {
        this.objectURLs.forEach(url => {
            URL.revokeObjectURL(url);
        });
        this.objectURLs.clear();
    }

    /**
     * Debounce para optimizar eventos frecuentes
     */
    debounce(func, wait = null) {
        wait = wait || this.config.debounceDelay;
        let timeout;

        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };

            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    /**
     * Throttle para limitar frecuencia de ejecución
     */
    throttle(func, limit = 16) {
        let inThrottle;

        return function executedFunction(...args) {
            if (!inThrottle) {
                func(...args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }

    /**
     * Verifica si el dispositivo soporta una característica
     */
    supports(feature) {
        const features = {
            webrtc: () => !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia),
            mediarecorder: () => typeof MediaRecorder !== 'undefined',
            webgl: () => {
                try {
                    const canvas = document.createElement('canvas');
                    return !!(canvas.getContext('webgl') || canvas.getContext('experimental-webgl'));
                } catch (e) {
                    return false;
                }
            },
            touchevents: () => 'ontouchstart' in window || navigator.maxTouchPoints > 0,
            vibration: () => 'vibrate' in navigator,
            fullscreen: () => document.fullscreenEnabled || document.webkitFullscreenEnabled,
            webaudio: () => !!(window.AudioContext || window.webkitAudioContext),
            indexeddb: () => !!window.indexedDB
        };

        return features[feature] ? features[feature]() : false;
    }

    /**
     * Obtiene información del dispositivo
     */
    getDeviceInfo() {
        return {
            isMobile: this.isMobile,
            isIOS: this.isIOS,
            isAndroid: this.isAndroid,
            isSafari: this.isSafari,
            screenWidth: window.screen.width,
            screenHeight: window.screen.height,
            pixelRatio: window.devicePixelRatio || 1,
            orientation: window.innerWidth > window.innerHeight ? 'landscape' : 'portrait',
            touchSupport: this.supports('touchevents'),
            webRTCSupport: this.supports('webrtc'),
            mediaRecorderSupport: this.supports('mediarecorder')
        };
    }

    /**
     * Optimiza el rendimiento del canvas
     */
    optimizeCanvas(canvas) {
        const ctx = canvas.getContext('2d');

        // Deshabilitar suavizado de imagen en dispositivos móviles para mejor rendimiento
        if (this.isMobile) {
            ctx.imageSmoothingEnabled = true;
            ctx.imageSmoothingQuality = 'medium';
        } else {
            ctx.imageSmoothingEnabled = true;
            ctx.imageSmoothingQuality = 'high';
        }

        return ctx;
    }

    /**
     * Request Animation Frame con fallback
     */
    requestAnimationFrame(callback) {
        return (window.requestAnimationFrame ||
            window.webkitRequestAnimationFrame ||
            window.mozRequestAnimationFrame ||
            ((cb) => setTimeout(cb, 16)))(callback);
    }

    /**
     * Vibra el dispositivo (si es soportado)
     */
    vibrate(pattern = 50) {
        if (this.supports('vibration')) {
            navigator.vibrate(pattern);
        }
    }

    /**
     * Solicita pantalla completa (con fallbacks)
     */
    requestFullscreen(element) {
        element = element || document.documentElement;

        if (element.requestFullscreen) {
            return element.requestFullscreen();
        } else if (element.webkitRequestFullscreen) {
            return element.webkitRequestFullscreen();
        } else if (element.mozRequestFullScreen) {
            return element.mozRequestFullScreen();
        } else if (element.msRequestFullscreen) {
            return element.msRequestFullscreen();
        }

        return Promise.reject(new Error('Fullscreen not supported'));
    }

    /**
     * Sale de pantalla completa
     */
    exitFullscreen() {
        if (document.exitFullscreen) {
            return document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
            return document.webkitExitFullscreen();
        } else if (document.mozCancelFullScreen) {
            return document.mozCancelFullScreen();
        } else if (document.msExitFullscreen) {
            return document.msExitFullscreen();
        }

        return Promise.reject(new Error('Fullscreen not supported'));
    }

    /**
     * Limpia recursos al destruir
     */
    destroy() {
        this.cleanupObjectURLs();
    }
}

/**
 * PerformanceMonitor - Monitor de rendimiento
 */
class PerformanceMonitor {
    constructor() {
        this.fps = 0;
        this.frameCount = 0;
        this.lastTime = performance.now();
        this.isMonitoring = false;
    }

    start() {
        this.isMonitoring = true;
        this.measure();
    }

    stop() {
        this.isMonitoring = false;
    }

    measure() {
        if (!this.isMonitoring) return;

        this.frameCount++;
        const currentTime = performance.now();
        const elapsed = currentTime - this.lastTime;

        if (elapsed >= 1000) {
            this.fps = Math.round((this.frameCount * 1000) / elapsed);
            this.frameCount = 0;
            this.lastTime = currentTime;

            // Advertir si FPS es bajo
            if (this.fps < 30) {
                console.warn(`FPS bajo detectado: ${this.fps}`);
                window.dispatchEvent(new CustomEvent('lowfps', { detail: { fps: this.fps } }));
            }
        }

        requestAnimationFrame(() => this.measure());
    }

    getFPS() {
        return this.fps;
    }
}

/**
 * NetworkStatus - Estado de la red
 */
class NetworkStatus {
    constructor() {
        this.isOnline = navigator.onLine;
        this.connectionType = this.getConnectionType();
        this.listeners = [];

        this.init();
    }

    init() {
        window.addEventListener('online', () => {
            this.isOnline = true;
            this.notify();
        });

        window.addEventListener('offline', () => {
            this.isOnline = false;
            this.notify();
        });

        // Monitorear cambios de conexión
        if ('connection' in navigator) {
            navigator.connection.addEventListener('change', () => {
                this.connectionType = this.getConnectionType();
                this.notify();
            });
        }
    }

    getConnectionType() {
        if ('connection' in navigator) {
            return {
                effectiveType: navigator.connection.effectiveType,
                downlink: navigator.connection.downlink,
                rtt: navigator.connection.rtt,
                saveData: navigator.connection.saveData
            };
        }
        return null;
    }

    isSlowConnection() {
        if (!this.connectionType) return false;
        return ['slow-2g', '2g'].includes(this.connectionType.effectiveType);
    }

    onChange(callback) {
        this.listeners.push(callback);
    }

    notify() {
        this.listeners.forEach(cb => cb({
            isOnline: this.isOnline,
            connectionType: this.connectionType
        }));
    }
}

// Instancia global del optimizador
let mobileOptimizer = null;

function getMobileOptimizer() {
    if (!mobileOptimizer) {
        mobileOptimizer = new MobileOptimizer();
    }
    return mobileOptimizer;
}
