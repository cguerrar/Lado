/**
 * iOS Audio Compatibility Fix
 * Soluciona problemas de reproducción de audio en iOS Safari
 *
 * PROBLEMAS QUE SOLUCIONA:
 * 1. AudioContext suspended hasta interacción del usuario
 * 2. Autoplay bloqueado sin interacción
 * 3. Volume programático ignorado en iOS
 * 4. Audio pausado al ir a background
 */

(function() {
    'use strict';

    // Detectar iOS
    const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
    const isSafari = /^((?!chrome|android).)*safari/i.test(navigator.userAgent);

    // Estado global
    let audioContextUnlocked = false;
    let globalAudioContext = null;
    let pendingAudioElements = [];

    /**
     * Inicializar y desbloquear AudioContext
     */
    function initAudioContext() {
        if (globalAudioContext) return globalAudioContext;

        try {
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            globalAudioContext = new AudioContextClass();
            console.log('[iOS Audio Fix] AudioContext creado, estado:', globalAudioContext.state);
        } catch (e) {
            console.warn('[iOS Audio Fix] No se pudo crear AudioContext:', e);
        }

        return globalAudioContext;
    }

    /**
     * Resumir AudioContext después de interacción del usuario
     */
    async function resumeAudioContext() {
        if (audioContextUnlocked) return true;

        const ctx = initAudioContext();
        if (!ctx) return false;

        if (ctx.state === 'suspended') {
            try {
                await ctx.resume();
                console.log('[iOS Audio Fix] AudioContext resumed exitosamente');
            } catch (e) {
                console.error('[iOS Audio Fix] Error al resumir AudioContext:', e);
                return false;
            }
        }

        audioContextUnlocked = true;

        // Reproducir sonido silencioso para desbloquear completamente
        try {
            const buffer = ctx.createBuffer(1, 1, 22050);
            const source = ctx.createBufferSource();
            source.buffer = buffer;
            source.connect(ctx.destination);
            source.start(0);
        } catch (e) {
            // Ignorar errores del buffer silencioso
        }

        // Intentar reproducir elementos de audio pendientes
        pendingAudioElements.forEach(audio => {
            if (audio && audio.paused && audio.dataset.shouldPlay === 'true') {
                playAudioSafely(audio);
            }
        });
        pendingAudioElements = [];

        return true;
    }

    /**
     * Reproducir audio de forma segura en iOS
     */
    async function playAudioSafely(audioElement) {
        if (!audioElement) return false;

        // Asegurar atributos necesarios para iOS
        audioElement.setAttribute('playsinline', '');
        audioElement.setAttribute('webkit-playsinline', '');

        // Si es iOS y el contexto no está desbloqueado, marcar para después
        if (isIOS && !audioContextUnlocked) {
            audioElement.dataset.shouldPlay = 'true';
            pendingAudioElements.push(audioElement);
            console.log('[iOS Audio Fix] Audio encolado, esperando interacción del usuario');
            return false;
        }

        try {
            // Asegurar que AudioContext está activo
            if (globalAudioContext && globalAudioContext.state === 'suspended') {
                await globalAudioContext.resume();
            }

            const playPromise = audioElement.play();
            if (playPromise !== undefined) {
                await playPromise;
                return true;
            }
        } catch (error) {
            console.warn('[iOS Audio Fix] No se pudo reproducir audio:', error.message);

            // En iOS, si falla, marcar para reproducir después de interacción
            if (isIOS) {
                audioElement.dataset.shouldPlay = 'true';
                pendingAudioElements.push(audioElement);
            }
            return false;
        }

        return true;
    }

    /**
     * Configurar volumen (con advertencia para iOS)
     */
    function setVolumeSafely(audioElement, volume) {
        if (!audioElement) return;

        const normalizedVolume = Math.max(0, Math.min(1, volume));

        // En iOS, el volumen programático es ignorado pero lo intentamos igual
        audioElement.volume = normalizedVolume;

        if (isIOS) {
            // Mostrar indicador visual de que iOS controla el volumen
            console.log('[iOS Audio Fix] Nota: iOS controla el volumen con botones físicos');
        }
    }

    /**
     * Manejar visibilidad (background/foreground)
     */
    function setupVisibilityHandling() {
        let wasPlayingBeforeHidden = new Map();

        document.addEventListener('visibilitychange', () => {
            const audioElements = document.querySelectorAll('audio, video');

            if (document.hidden) {
                // Guardando estado antes de ir a background
                audioElements.forEach(el => {
                    wasPlayingBeforeHidden.set(el, !el.paused);
                });
            } else {
                // Volviendo del background
                audioElements.forEach(el => {
                    if (wasPlayingBeforeHidden.get(el) && el.paused) {
                        playAudioSafely(el);
                    }
                });
                wasPlayingBeforeHidden.clear();
            }
        });
    }

    /**
     * Configurar listeners de interacción del usuario
     */
    function setupUserInteractionListeners() {
        const unlockAudio = async (e) => {
            await resumeAudioContext();

            // Remover listeners después de primer uso exitoso
            if (audioContextUnlocked) {
                document.removeEventListener('touchstart', unlockAudio);
                document.removeEventListener('touchend', unlockAudio);
                document.removeEventListener('click', unlockAudio);
                document.removeEventListener('keydown', unlockAudio);
            }
        };

        // Escuchar múltiples tipos de interacción
        document.addEventListener('touchstart', unlockAudio, { passive: true });
        document.addEventListener('touchend', unlockAudio, { passive: true });
        document.addEventListener('click', unlockAudio, { passive: true });
        document.addEventListener('keydown', unlockAudio, { passive: true });
    }

    /**
     * Parchear HTMLAudioElement.prototype.play para iOS
     */
    function patchAudioPlay() {
        const originalPlay = HTMLAudioElement.prototype.play;

        HTMLAudioElement.prototype.play = function() {
            // Asegurar atributos para iOS
            this.setAttribute('playsinline', '');
            this.setAttribute('webkit-playsinline', '');

            // Si es iOS y no está desbloqueado, intentar desbloquear primero
            if (isIOS && !audioContextUnlocked && globalAudioContext) {
                resumeAudioContext();
            }

            return originalPlay.call(this);
        };
    }

    /**
     * Mejorar elementos de audio existentes
     */
    function enhanceExistingAudioElements() {
        document.querySelectorAll('audio').forEach(audio => {
            // Agregar atributos de iOS
            audio.setAttribute('playsinline', '');
            audio.setAttribute('webkit-playsinline', '');

            // Si tiene preload="none", cambiar a "metadata" para mejor UX
            if (audio.preload === 'none') {
                audio.preload = 'metadata';
            }
        });
    }

    /**
     * Observar nuevos elementos de audio agregados al DOM
     */
    function observeNewAudioElements() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeName === 'AUDIO') {
                        node.setAttribute('playsinline', '');
                        node.setAttribute('webkit-playsinline', '');
                    }
                    // También buscar audio dentro de nodos agregados
                    if (node.querySelectorAll) {
                        node.querySelectorAll('audio').forEach(audio => {
                            audio.setAttribute('playsinline', '');
                            audio.setAttribute('webkit-playsinline', '');
                        });
                    }
                });
            });
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    /**
     * API Pública
     */
    window.iOSAudioFix = {
        isIOS: isIOS,
        isSafari: isSafari,
        isUnlocked: () => audioContextUnlocked,
        getAudioContext: () => globalAudioContext,
        resume: resumeAudioContext,
        play: playAudioSafely,
        setVolume: setVolumeSafely,

        // Método para mostrar UI de "toca para reproducir" si es necesario
        needsUserInteraction: () => isIOS && !audioContextUnlocked
    };

    /**
     * Inicialización
     */
    function init() {
        console.log('[iOS Audio Fix] Iniciando...', { isIOS, isSafari });

        // Inicializar AudioContext
        initAudioContext();

        // Configurar listeners
        setupUserInteractionListeners();
        setupVisibilityHandling();

        // Parchear play()
        patchAudioPlay();

        // Esperar a que el DOM esté listo
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                enhanceExistingAudioElements();
                observeNewAudioElements();
            });
        } else {
            enhanceExistingAudioElements();
            observeNewAudioElements();
        }

        console.log('[iOS Audio Fix] Inicializado correctamente');
    }

    // Iniciar
    init();

})();
