/**
 * LADO - Mobile Enhancements
 * Mejoras específicas para experiencia móvil en iOS y Android
 * Compatible con Safari, Chrome, Firefox móvil
 */

(function() {
    'use strict';

    // ========================================
    // CARRUSEL SWIPE - Touch Navigation
    // ========================================
    const CarouselSwipe = {
        init: function() {
            // Solo en dispositivos táctiles
            if (!('ontouchstart' in window)) return;

            // Observar carruseles nuevos agregados al DOM
            this.observeNewCarousels();

            // Inicializar carruseles existentes
            this.initializeExistingCarousels();
        },

        observeNewCarousels: function() {
            const observer = new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === 1) {
                            const carousels = node.querySelectorAll ?
                                node.querySelectorAll('.post-media-carousel, .carousel-container, .fs-carousel-container') : [];
                            carousels.forEach(carousel => this.attachSwipe(carousel));

                            if (node.classList && (node.classList.contains('post-media-carousel') ||
                                node.classList.contains('carousel-container'))) {
                                this.attachSwipe(node);
                            }
                        }
                    });
                });
            });

            observer.observe(document.body, { childList: true, subtree: true });
        },

        initializeExistingCarousels: function() {
            document.querySelectorAll('.post-media-carousel, .carousel-container, .fs-carousel-container')
                .forEach(carousel => this.attachSwipe(carousel));
        },

        attachSwipe: function(carousel) {
            if (carousel.dataset.swipeInitialized) return;
            carousel.dataset.swipeInitialized = 'true';

            let touchStartX = 0;
            let touchStartY = 0;
            let touchEndX = 0;
            let touchEndY = 0;
            let isSwiping = false;

            carousel.addEventListener('touchstart', (e) => {
                touchStartX = e.touches[0].clientX;
                touchStartY = e.touches[0].clientY;
                isSwiping = true;
            }, { passive: true });

            carousel.addEventListener('touchmove', (e) => {
                if (!isSwiping) return;
                touchEndX = e.touches[0].clientX;
                touchEndY = e.touches[0].clientY;
            }, { passive: true });

            carousel.addEventListener('touchend', (e) => {
                if (!isSwiping) return;
                isSwiping = false;

                const deltaX = touchEndX - touchStartX;
                const deltaY = touchEndY - touchStartY;

                // Solo procesar si el movimiento horizontal es mayor que el vertical
                if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 50) {
                    const postId = carousel.dataset.postId || carousel.closest('[data-post-id]')?.dataset.postId;

                    if (deltaX > 0) {
                        // Swipe derecha - anterior
                        this.navigateCarousel(carousel, postId, -1);
                    } else {
                        // Swipe izquierda - siguiente
                        this.navigateCarousel(carousel, postId, 1);
                    }

                    // Haptic feedback
                    if (window.LadoHaptic) {
                        window.LadoHaptic.light();
                    }
                }
            }, { passive: true });
        },

        navigateCarousel: function(carousel, postId, direction) {
            // Intentar usar función global si existe
            if (window.fsCarouselNav && postId) {
                window.fsCarouselNav(postId, direction);
                return;
            }

            // Fallback: buscar botones de navegación y clickear
            const navClass = direction > 0 ? '.carousel-next, .fs-carousel-next' : '.carousel-prev, .fs-carousel-prev';
            const navBtn = carousel.querySelector(navClass);
            if (navBtn) {
                navBtn.click();
            }

            // Alternativa: manipular scroll directamente
            const items = carousel.querySelector('.carousel-inner, .fs-carousel-inner');
            if (items) {
                const itemWidth = items.firstElementChild?.offsetWidth || 0;
                if (itemWidth > 0) {
                    items.scrollBy({
                        left: direction * itemWidth,
                        behavior: 'smooth'
                    });
                }
            }
        }
    };

    // ========================================
    // VIDEO SWIPE - Vertical Navigation (Reels)
    // ========================================
    const VideoSwipe = {
        init: function() {
            if (!('ontouchstart' in window)) return;

            const containers = document.querySelectorAll('.fullscreen-feed, .reels-container');
            containers.forEach(container => this.attachSwipe(container));
        },

        attachSwipe: function(container) {
            if (container.dataset.videoSwipeInit) return;
            container.dataset.videoSwipeInit = 'true';

            let touchStartY = 0;
            let touchEndY = 0;

            container.addEventListener('touchstart', (e) => {
                touchStartY = e.touches[0].clientY;
            }, { passive: true });

            container.addEventListener('touchend', (e) => {
                touchEndY = e.changedTouches[0].clientY;
                const deltaY = touchEndY - touchStartY;

                if (Math.abs(deltaY) > 100) {
                    if (deltaY < 0) {
                        // Swipe arriba - siguiente video
                        this.nextVideo();
                    } else {
                        // Swipe abajo - video anterior
                        this.prevVideo();
                    }
                }
            }, { passive: true });
        },

        nextVideo: function() {
            const nextBtn = document.querySelector('.fs-nav-next, .reels-nav-next, [data-action="next-video"]');
            if (nextBtn) nextBtn.click();
        },

        prevVideo: function() {
            const prevBtn = document.querySelector('.fs-nav-prev, .reels-nav-prev, [data-action="prev-video"]');
            if (prevBtn) prevBtn.click();
        }
    };

    // ========================================
    // SMOOTH SCROLL TO TOP
    // ========================================
    const ScrollToTop = {
        init: function() {
            // Doble tap en el header para volver arriba
            const headers = document.querySelectorAll('.top-navbar, .mobile-header');
            headers.forEach(header => {
                let lastTap = 0;
                header.addEventListener('click', (e) => {
                    const now = Date.now();
                    if (now - lastTap < 300) {
                        window.scrollTo({
                            top: 0,
                            behavior: 'smooth'
                        });
                        if (window.LadoHaptic) {
                            window.LadoHaptic.light();
                        }
                    }
                    lastTap = now;
                });
            });

            // También para bottom nav item de Home
            const homeNav = document.querySelector('[data-nav="feed"]');
            if (homeNav) {
                homeNav.addEventListener('click', (e) => {
                    // Si ya estamos en /Feed, scroll to top en lugar de navegar
                    if (window.location.pathname.toLowerCase() === '/feed' ||
                        window.location.pathname.toLowerCase() === '/feed/index') {
                        e.preventDefault();
                        window.scrollTo({
                            top: 0,
                            behavior: 'smooth'
                        });
                        if (window.LadoHaptic) {
                            window.LadoHaptic.light();
                        }
                    }
                });
            }
        }
    };

    // ========================================
    // IMAGE PINCH TO ZOOM
    // ========================================
    const PinchZoom = {
        init: function() {
            if (!('ontouchstart' in window)) return;

            document.querySelectorAll('.post-media img, .content-media img')
                .forEach(img => this.attachZoom(img));

            // Observer para imágenes nuevas
            const observer = new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === 1) {
                            const images = node.querySelectorAll ?
                                node.querySelectorAll('.post-media img, .content-media img') : [];
                            images.forEach(img => this.attachZoom(img));
                        }
                    });
                });
            });

            observer.observe(document.body, { childList: true, subtree: true });
        },

        attachZoom: function(img) {
            if (img.dataset.zoomInit) return;
            img.dataset.zoomInit = 'true';

            let scale = 1;
            let initialDistance = 0;

            img.addEventListener('touchstart', (e) => {
                if (e.touches.length === 2) {
                    initialDistance = this.getDistance(e.touches[0], e.touches[1]);
                }
            }, { passive: true });

            img.addEventListener('touchmove', (e) => {
                if (e.touches.length === 2) {
                    e.preventDefault();
                    const currentDistance = this.getDistance(e.touches[0], e.touches[1]);
                    scale = Math.min(Math.max(scale * (currentDistance / initialDistance), 1), 3);
                    img.style.transform = `scale(${scale})`;
                    initialDistance = currentDistance;
                }
            }, { passive: false });

            img.addEventListener('touchend', (e) => {
                if (e.touches.length < 2) {
                    // Volver a escala normal con animación
                    img.style.transition = 'transform 0.3s ease';
                    img.style.transform = 'scale(1)';
                    scale = 1;
                    setTimeout(() => {
                        img.style.transition = '';
                    }, 300);
                }
            }, { passive: true });
        },

        getDistance: function(touch1, touch2) {
            const dx = touch1.clientX - touch2.clientX;
            const dy = touch1.clientY - touch2.clientY;
            return Math.sqrt(dx * dx + dy * dy);
        }
    };

    // ========================================
    // OFFLINE QUEUE - Guardar acciones offline
    // ========================================
    const OfflineQueue = {
        dbName: 'lado-offline',
        dbVersion: 1,

        init: async function() {
            // Inicializar IndexedDB para cola offline
            try {
                await this.openDB();
            } catch (error) {
            }
        },

        openDB: function() {
            return new Promise((resolve, reject) => {
                const request = indexedDB.open(this.dbName, this.dbVersion);

                request.onerror = () => reject(request.error);
                request.onsuccess = () => resolve(request.result);

                request.onupgradeneeded = (event) => {
                    const db = event.target.result;

                    if (!db.objectStoreNames.contains('pending-likes')) {
                        db.createObjectStore('pending-likes', { keyPath: 'id', autoIncrement: true });
                    }
                    if (!db.objectStoreNames.contains('pending-comments')) {
                        db.createObjectStore('pending-comments', { keyPath: 'id', autoIncrement: true });
                    }
                    if (!db.objectStoreNames.contains('pending-follows')) {
                        db.createObjectStore('pending-follows', { keyPath: 'id', autoIncrement: true });
                    }
                };
            });
        },

        addToQueue: async function(store, data) {
            try {
                const db = await this.openDB();
                const tx = db.transaction(store, 'readwrite');
                const objectStore = tx.objectStore(store);

                return new Promise((resolve, reject) => {
                    const request = objectStore.add({
                        ...data,
                        timestamp: Date.now()
                    });
                    request.onsuccess = () => {
                        resolve(request.result);
                        // Registrar para background sync
                        if ('serviceWorker' in navigator && 'SyncManager' in window) {
                            navigator.serviceWorker.ready.then(registration => {
                                registration.sync.register(`sync-${store.replace('pending-', '')}`);
                            });
                        }
                    };
                    request.onerror = () => reject(request.error);
                });
            } catch (error) {
            }
        }
    };

    // ========================================
    // PERFORMANCE MONITORING
    // ========================================
    const PerformanceMonitor = {
        init: function() {
            if (!('PerformanceObserver' in window)) return;

            // Observar Long Tasks (más de 50ms)
            try {
                const longTaskObserver = new PerformanceObserver((list) => {
                    for (const entry of list.getEntries()) {
                        if (entry.duration > 100) {
                        }
                    }
                });
                longTaskObserver.observe({ entryTypes: ['longtask'] });
            } catch (e) {
                // Long tasks no soportados
            }

            // Reportar métricas LCP
            try {
                const lcpObserver = new PerformanceObserver((list) => {
                    const entries = list.getEntries();
                    const lastEntry = entries[entries.length - 1];
                });
                lcpObserver.observe({ entryTypes: ['largest-contentful-paint'] });
            } catch (e) {
                // LCP no soportado
            }
        }
    };

    // ========================================
    // NETWORK STATUS
    // ========================================
    const NetworkStatus = {
        init: function() {
            if (!('onLine' in navigator)) return;

            window.addEventListener('online', () => {
                this.showStatus(true);
                // Sincronizar cola offline
                if ('serviceWorker' in navigator && 'SyncManager' in window) {
                    navigator.serviceWorker.ready.then(registration => {
                        registration.sync.register('sync-likes');
                        registration.sync.register('sync-comments');
                        registration.sync.register('sync-follows');
                    });
                }
            });

            window.addEventListener('offline', () => {
                this.showStatus(false);
            });
        },

        showStatus: function(isOnline) {
            // Crear o actualizar banner de estado
            let banner = document.getElementById('network-status-banner');

            if (!banner) {
                banner = document.createElement('div');
                banner.id = 'network-status-banner';
                banner.style.cssText = `
                    position: fixed;
                    top: 0;
                    left: 0;
                    right: 0;
                    padding: 8px 16px;
                    text-align: center;
                    font-size: 13px;
                    font-weight: 500;
                    z-index: 99999;
                    transform: translateY(-100%);
                    transition: transform 0.3s ease;
                `;
                document.body.appendChild(banner);
            }

            if (isOnline) {
                banner.style.background = '#10b981';
                banner.style.color = 'white';
                banner.textContent = 'Conexion restaurada';
                banner.style.transform = 'translateY(0)';

                setTimeout(() => {
                    banner.style.transform = 'translateY(-100%)';
                }, 2000);
            } else {
                banner.style.background = '#ef4444';
                banner.style.color = 'white';
                banner.textContent = 'Sin conexion - Los cambios se guardaran localmente';
                banner.style.transform = 'translateY(0)';
            }
        }
    };

    // ========================================
    // INICIALIZACIÓN
    // ========================================
    function init() {
        // Esperar a que el DOM esté listo
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', initAll);
        } else {
            initAll();
        }
    }

    function initAll() {
        CarouselSwipe.init();
        VideoSwipe.init();
        ScrollToTop.init();
        PinchZoom.init();
        OfflineQueue.init();
        NetworkStatus.init();

        // Performance monitoring solo en desarrollo
        if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
            PerformanceMonitor.init();
        }
    }

    // Exportar para uso global
    window.LadoMobile = {
        CarouselSwipe,
        VideoSwipe,
        ScrollToTop,
        PinchZoom,
        OfflineQueue,
        NetworkStatus
    };

    init();
})();
