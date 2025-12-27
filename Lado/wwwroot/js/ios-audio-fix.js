/**
 * iOS Media Compatibility Fix v5.0
 * Compatible con iOS 14-18, iPhone, iPad, iPod
 * Safari, Chrome, Firefox en iOS (todos usan WebKit)
 *
 * ESTRATEGIAS:
 * 1. Detección precisa de dispositivo y versión
 * 2. Múltiples intentos de reproducción con backoff
 * 3. Precarga agresiva de videos
 * 4. Desbloqueo por interacción con múltiples técnicas
 * 5. Fallback visual cuando falla autoplay
 */

(function() {
    'use strict';

    // ========================================
    // DETECCIÓN DE DISPOSITIVO (MEJORADA)
    // ========================================
    const ua = navigator.userAgent || '';
    const platform = navigator.platform || '';

    // Detectar iOS/iPadOS de múltiples formas
    const isIPhone = /iPhone/.test(ua);
    const isIPad = /iPad/.test(ua) || (platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const isIPod = /iPod/.test(ua);
    const isMSStream = !!window.MSStream;
    const isIOS = (isIPhone || isIPad || isIPod) && !isMSStream;

    // Versión de iOS
    const iosMatch = ua.match(/(?:iPhone|CPU) OS (\d+)[_.](\d+)/);
    const iosVersion = iosMatch ? {
        major: parseInt(iosMatch[1], 10),
        minor: parseInt(iosMatch[2], 10),
        full: parseInt(iosMatch[1], 10) + (parseInt(iosMatch[2], 10) / 10)
    } : { major: 0, minor: 0, full: 0 };

    // Detectar Safari vs otros browsers
    const isSafari = /Safari/.test(ua) && !/Chrome|CriOS|FxiOS/.test(ua);
    const isChrome = /CriOS/.test(ua);
    const isFirefox = /FxiOS/.test(ua);

    // Detectar modo de bajo consumo o datos (heurística)
    const isLowPowerMode = navigator.getBattery ? null : undefined;

    // Log de diagnóstico
    console.log('[iOS Media v5]', {
        device: isIPhone ? 'iPhone' : isIPad ? 'iPad' : isIPod ? 'iPod' : 'Unknown',
        iOS: isIOS,
        version: iosVersion.full,
        browser: isSafari ? 'Safari' : isChrome ? 'Chrome' : isFirefox ? 'Firefox' : 'Other'
    });

    // ========================================
    // ESTADO GLOBAL
    // ========================================
    let isUnlocked = false;
    let hasInteracted = false;
    let audioContext = null;
    const videoQueue = new Map(); // videoElement -> { attempts, lastAttempt }
    const audioQueue = [];
    const MAX_ATTEMPTS = 3;
    const RETRY_DELAY = 500;

    // ========================================
    // LOGGING
    // ========================================
    function log(...args) {
        console.log('[iOS]', ...args);
        if (window.iOSDebug) window.iOSDebug(args.join(' '));
    }

    // ========================================
    // AUDIO CONTEXT
    // ========================================
    function createAudioContext() {
        if (audioContext) return audioContext;
        try {
            const AC = window.AudioContext || window.webkitAudioContext;
            audioContext = new AC();
            log('AudioContext creado:', audioContext.state);
        } catch (e) {
            log('Error creando AudioContext');
        }
        return audioContext;
    }

    async function resumeAudioContext() {
        const ctx = createAudioContext();
        if (!ctx) return false;

        if (ctx.state === 'suspended') {
            try {
                await ctx.resume();
                log('AudioContext resumed');
            } catch (e) {
                return false;
            }
        }

        // Truco: reproducir sonido silencioso
        try {
            const buffer = ctx.createBuffer(1, 1, 22050);
            const source = ctx.createBufferSource();
            source.buffer = buffer;
            source.connect(ctx.destination);
            source.start(0);
        } catch (e) {}

        return ctx.state === 'running';
    }

    // ========================================
    // PREPARACIÓN DE VIDEO
    // ========================================
    function prepareVideo(video) {
        if (!video || video.dataset.iosPrepared) return;
        video.dataset.iosPrepared = 'true';

        // Atributos CRÍTICOS para iOS
        const attrs = {
            'playsinline': '',
            'webkit-playsinline': '',
            'x-webkit-airplay': 'allow',
            'muted': '',
            'autoplay': ''
        };

        Object.entries(attrs).forEach(([key, val]) => {
            video.setAttribute(key, val);
        });

        // Propiedades JavaScript
        video.playsInline = true;
        video.muted = true;
        video.defaultMuted = true;
        video.autoplay = true;

        // Preload agresivo para iOS
        if (video.preload !== 'auto') {
            video.preload = 'auto';
        }

        // Eventos de debug
        video.addEventListener('loadstart', () => log('loadstart'));
        video.addEventListener('canplay', () => log('canplay'));
        video.addEventListener('playing', () => {
            log('playing ✓');
            updateVideoUI(video, true);
            videoQueue.delete(video);
        });
        video.addEventListener('pause', () => {
            if (!video.ended) log('pause');
        });
        video.addEventListener('error', (e) => {
            log('error:', video.error?.message || 'unknown');
        });
        video.addEventListener('stalled', () => {
            log('stalled - reintentando...');
            if (!video.paused) {
                setTimeout(() => {
                    if (!video.paused && video.readyState < 3) {
                        video.load();
                        playVideo(video);
                    }
                }, 1000);
            }
        });
    }

    // ========================================
    // REPRODUCCIÓN DE VIDEO
    // ========================================
    async function playVideo(video, options = {}) {
        if (!video) return false;

        const { force = false, attempt = 1 } = options;

        prepareVideo(video);

        // Asegurar muted
        video.muted = true;

        // Registrar intento
        const queueEntry = videoQueue.get(video) || { attempts: 0, lastAttempt: 0 };
        queueEntry.attempts = attempt;
        queueEntry.lastAttempt = Date.now();
        videoQueue.set(video, queueEntry);

        // Esperar carga si es necesario
        if (video.readyState < 2) {
            log('Cargando video... (readyState:', video.readyState + ')');

            // Forzar carga
            if (video.readyState === 0) {
                video.load();
            }

            // Esperar con timeout
            const loaded = await Promise.race([
                new Promise(resolve => {
                    const onLoad = () => {
                        video.removeEventListener('canplay', onLoad);
                        video.removeEventListener('loadeddata', onLoad);
                        resolve(true);
                    };
                    video.addEventListener('canplay', onLoad);
                    video.addEventListener('loadeddata', onLoad);
                }),
                new Promise(resolve => setTimeout(() => resolve(false), 5000))
            ]);

            if (!loaded) {
                log('Timeout cargando video');
                if (attempt < MAX_ATTEMPTS) {
                    await new Promise(r => setTimeout(r, RETRY_DELAY));
                    return playVideo(video, { force, attempt: attempt + 1 });
                }
                updateVideoUI(video, false, true);
                return false;
            }
        }

        try {
            log('Reproduciendo... (intento ' + attempt + ')');

            // Estrategia 1: play() directo
            const playPromise = video.play();

            if (playPromise !== undefined) {
                await playPromise;
            }

            log('Video OK ✓');
            updateVideoUI(video, true);
            videoQueue.delete(video);
            return true;

        } catch (error) {
            log('Error:', error.name);

            if (error.name === 'NotAllowedError') {
                // Autoplay bloqueado - necesita interacción
                updateVideoUI(video, false, true);

                if (attempt < MAX_ATTEMPTS && hasInteracted) {
                    // Reintentar después de un delay
                    await new Promise(r => setTimeout(r, RETRY_DELAY));
                    return playVideo(video, { force, attempt: attempt + 1 });
                }
            } else if (error.name === 'AbortError' && attempt < MAX_ATTEMPTS) {
                // Carga interrumpida - reintentar
                await new Promise(r => setTimeout(r, RETRY_DELAY));
                return playVideo(video, { force, attempt: attempt + 1 });
            }

            return false;
        }
    }

    function pauseVideo(video) {
        if (!video) return;
        video.pause();
        updateVideoUI(video, false);
    }

    function toggleVideo(video) {
        if (!video) return;

        // Pausar otros videos
        document.querySelectorAll('video.feed-video').forEach(v => {
            if (v !== video && !v.paused) {
                v.pause();
                updateVideoUI(v, false);
            }
        });

        if (video.paused) {
            playVideo(video, { force: true });
        } else {
            pauseVideo(video);
        }

        // Haptic feedback
        if (navigator.vibrate) navigator.vibrate(10);
    }

    function updateVideoUI(video, playing, needsTap = false) {
        const container = video.closest('.post-media');
        if (!container) return;

        container.classList.toggle('video-playing', playing);
        container.classList.toggle('video-paused', !playing);
        container.classList.toggle('needs-tap', needsTap && !playing);
    }

    // ========================================
    // AUDIO (MÚSICA EN FOTOS)
    // ========================================
    async function playAudio(audio, opts = {}) {
        if (!audio) return false;

        const { startTime = 0, volume = 0.7 } = opts;

        // Pausar otros audios
        document.querySelectorAll('.photo-audio-player').forEach(a => {
            if (a !== audio && !a.paused) a.pause();
        });

        audio.currentTime = startTime;
        audio.volume = Math.min(1, Math.max(0, volume));

        if (isIOS && !hasInteracted) {
            log('Audio pendiente - esperando interacción');
            audioQueue.push({ audio, opts });
            return false;
        }

        try {
            await audio.play();
            log('Audio OK ✓');
            return true;
        } catch (e) {
            log('Audio error:', e.name);
            audioQueue.push({ audio, opts });
            return false;
        }
    }

    function pauseAudio(audio) {
        if (audio) audio.pause();
    }

    // ========================================
    // DESBLOQUEO POR INTERACCIÓN
    // ========================================
    async function unlock() {
        // Solo ejecutar en iOS real
        if (!isIOS) {
            isUnlocked = true;
            hasInteracted = true;
            return true;
        }

        if (isUnlocked && hasInteracted) return true;

        log('Desbloqueando...');
        hasInteracted = true;

        // Resumir AudioContext
        const contextOk = await resumeAudioContext();
        log('AudioContext:', contextOk ? 'OK' : 'FAIL');

        // Reproducir video silencioso para desbloquear
        try {
            const v = document.createElement('video');
            v.muted = true;
            v.playsInline = true;
            v.style.cssText = 'position:fixed;top:-9999px;width:1px;height:1px;';
            v.src = 'data:video/mp4;base64,AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDEAAAAIZnJlZQAAA3RtZGF0AAACrwYF//+r3EXpvebZSLeWLNgg2SPu73gyNjQgLSBjb3JlIDE2NCByMzEwOCBhZjRmNjM0IC0gSC4yNjQvTVBFRy00IEFWQyBjb2RlYyAtIENvcHlsZWZ0IDIwMDMtMjAyNCAtIGh0dHA6Ly93d3cudmlkZW9sYW4ub3JnL3gyNjQuaHRtbCAtIG9wdGlvbnM6IGNhYmFjPTEgcmVmPTMgZGVibG9jaz0xOjA6MCBhbmFseXNlPTB4MzoweDExMyBtZT1oZXggc3VibWU9NyBwc3k9MSBwc3lfcmQ9MS4wMDowLjAwIG1peGVkX3JlZj0xIG1lX3JhbmdlPTE2IGNocm9tYV9tZT0xIHRyZWxsaXM9MSA4eDhkY3Q9MSBjcW09MCBkZWFkem9uZT0yMSwxMSBmYXN0X3Bza2lwPTEgY2hyb21hX3FwX29mZnNldD0tMiB0aHJlYWRzPTEyIGxvb2thaGVhZF90aHJlYWRzPTIgc2xpY2VkX3RocmVhZHM9MCBucj0wIGRlY2ltYXRlPTEgaW50ZXJsYWNlZD0wIGJsdXJheV9jb21wYXQ9MCBjb25zdHJhaW5lZF9pbnRyYT0wIGJmcmFtZXM9MyBiX3B5cmFtaWQ9MiBiX2FkYXB0PTEgYl9iaWFzPTAgZGlyZWN0PTEgd2VpZ2h0Yj0xIG9wZW5fZ29wPTAgd2VpZ2h0cD0yIGtleWludD0yNTAga2V5aW50X21pbj0yNSBzY2VuZWN1dD00MCBpbnRyYV9yZWZyZXNoPTAgcmNfbG9va2FoZWFkPTQwIHJjPWNyZiBtYnRyZWU9MSBjcmY9MjMuMCBxY29tcD0wLjYwIHFwbWluPTAgcXBtYXg9NjkgcXBzdGVwPTQgaXBfcmF0aW89MS40MCBhcT0xOjEuMDAAgAAAAA9liIQAM///7N/y1AAMWTAAAAMAUAAAMAADAAA8YAgAJ/4oAArVPgvshQAAAAwAAAwBkYXRhAAAAAA==';
            document.body.appendChild(v);
            await v.play();
            v.remove();
            log('Video silencioso OK');
        } catch (e) {
            log('Video silencioso falló');
        }

        isUnlocked = true;

        // Procesar videos en cola
        log('Procesando', videoQueue.size, 'videos pendientes');
        for (const [video] of videoQueue) {
            if (document.body.contains(video)) {
                const rect = video.getBoundingClientRect();
                const visible = rect.top < window.innerHeight && rect.bottom > 0;
                if (visible) {
                    await playVideo(video, { force: true });
                }
            }
        }

        // Procesar audios en cola
        log('Procesando', audioQueue.length, 'audios pendientes');
        for (const { audio, opts } of audioQueue) {
            if (document.body.contains(audio)) {
                await playAudio(audio, opts);
            }
        }
        audioQueue.length = 0;

        // Quitar indicadores visuales
        document.querySelectorAll('.needs-tap').forEach(el => {
            el.classList.remove('needs-tap');
        });

        // Evento para otros scripts
        document.dispatchEvent(new Event('iOSMediaUnlocked'));

        log('Desbloqueado ✓');
        return true;
    }

    // ========================================
    // INICIALIZACIÓN
    // ========================================
    function init() {
        log('Inicializando v5...');
        log('iOS:', isIOS, 'v' + iosVersion.full);

        // Solo agregar listeners y preparar videos en iOS real
        if (!isIOS) {
            log('No es iOS - saltando inicialización');
            isUnlocked = true;
            hasInteracted = true;
            return;
        }

        // Listeners de interacción (solo iOS)
        const interactionEvents = ['touchstart', 'touchend', 'click', 'pointerdown'];
        const handleInteraction = (e) => {
            unlock();
            // Mantener listeners para futuras interacciones (no remover)
        };

        interactionEvents.forEach(event => {
            document.addEventListener(event, handleInteraction, { passive: true, capture: true });
        });

        // Preparar videos existentes
        const prepareAll = () => {
            document.querySelectorAll('video').forEach(prepareVideo);
            log('Videos preparados:', document.querySelectorAll('video').length);
        };

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', prepareAll);
        } else {
            prepareAll();
        }

        // Observer para nuevos videos
        const observer = new MutationObserver(mutations => {
            mutations.forEach(m => {
                m.addedNodes.forEach(node => {
                    if (node.nodeName === 'VIDEO') {
                        prepareVideo(node);
                    }
                    if (node.querySelectorAll) {
                        node.querySelectorAll('video').forEach(prepareVideo);
                    }
                });
            });
        });

        if (document.body) {
            observer.observe(document.body, { childList: true, subtree: true });
        } else {
            document.addEventListener('DOMContentLoaded', () => {
                observer.observe(document.body, { childList: true, subtree: true });
            });
        }

        // Manejar visibilidad (cuando la app va a background)
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden && isIOS && isUnlocked) {
                // Volviendo del background - intentar resumir videos visibles
                document.querySelectorAll('video.feed-video').forEach(video => {
                    const rect = video.getBoundingClientRect();
                    const visible = rect.top < window.innerHeight && rect.bottom > 0;
                    if (visible && video.paused && !video.ended) {
                        const container = video.closest('.post-media');
                        if (!container?.classList.contains('video-paused')) {
                            playVideo(video);
                        }
                    }
                });
            }
        });

        log('Inicializado ✓');
    }

    // ========================================
    // API PÚBLICA
    // ========================================
    window.iOSMediaFix = window.iOSAudioFix = {
        // Detección
        isIOS,
        isIPhone,
        isIPad,
        iOSVersion: iosVersion.full,
        isSafari,

        // Estado
        isUnlocked: () => isUnlocked,
        hasUserInteracted: () => hasInteracted,
        needsUserInteraction: () => isIOS && !hasInteracted,

        // Video
        prepareVideo,
        playVideo,
        pauseVideo,
        toggleVideo,

        // Audio
        playAudio,
        pauseAudio,
        playPhotoMusic: playAudio,

        // Desbloqueo
        unlock,
        resume: resumeAudioContext,

        // Compat
        enhance: prepareVideo,
        play: (m) => m?.tagName === 'VIDEO' ? playVideo(m) : playAudio(m),
        pause: (m) => m?.tagName === 'VIDEO' ? pauseVideo(m) : pauseAudio(m)
    };

    // Iniciar
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
