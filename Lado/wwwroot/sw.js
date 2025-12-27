// Service Worker para LADO PWA v11.0
// Compatible con iOS Safari y Android Chrome
const CACHE_NAME = 'lado-cache-v11';
const OFFLINE_URL = '/offline.html';
const DYNAMIC_CACHE = 'lado-dynamic-v11';
const IMAGE_CACHE = 'lado-images-v11';

// Recursos para pre-cachear (shell de la app)
const PRECACHE_ASSETS = [
    '/',
    '/Feed',
    '/Feed/Explorar',
    '/offline.html',
    '/css/site.css',
    '/js/site.js',
    '/manifest.json',
    '/images/angelito.png',
    '/images/diablito.png',
    '/images/icons/icon-192x192.png',
    '/images/icons/icon-512x512.png',
    // Fuentes de Google
    'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap'
];

// Tiempo de vida de cache dinámico (24 horas)
const DYNAMIC_CACHE_MAX_AGE = 24 * 60 * 60 * 1000;
// Máximo de imágenes en cache
const IMAGE_CACHE_MAX_ITEMS = 100;

// Instalar - Pre-cachear recursos esenciales
self.addEventListener('install', (event) => {
    console.log('[SW] Instalando Service Worker...');

    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                console.log('[SW] Pre-cacheando recursos...');
                // Usar addAll con manejo de errores individual
                return Promise.allSettled(
                    PRECACHE_ASSETS.map(url =>
                        cache.add(url).catch(err => {
                            console.warn(`[SW] No se pudo cachear: ${url}`, err);
                        })
                    )
                );
            })
            .then(() => {
                console.log('[SW] Instalacion completada');
                // Activar inmediatamente sin esperar
                return self.skipWaiting();
            })
    );
});

// Activar - Limpiar caches antiguos
self.addEventListener('activate', (event) => {
    console.log('[SW] Activando Service Worker...');

    const VALID_CACHES = [CACHE_NAME, DYNAMIC_CACHE, IMAGE_CACHE];

    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames
                        .filter((name) => !VALID_CACHES.includes(name))
                        .map((name) => {
                            console.log(`[SW] Eliminando cache antiguo: ${name}`);
                            return caches.delete(name);
                        })
                );
            })
            .then(() => {
                console.log('[SW] Activacion completada');
                // Tomar control de todas las paginas inmediatamente
                return self.clients.claim();
            })
    );
});

// Fetch - Estrategias de cache
self.addEventListener('fetch', (event) => {
    const { request } = event;
    const url = new URL(request.url);

    // Ignorar requests que no son GET
    if (request.method !== 'GET') {
        return;
    }

    // Ignorar requests a otros dominios (excepto CDNs conocidos)
    if (!url.origin.includes(self.location.origin) &&
        !url.origin.includes('fonts.googleapis.com') &&
        !url.origin.includes('fonts.gstatic.com') &&
        !url.origin.includes('cdnjs.cloudflare.com')) {
        return;
    }

    // Ignorar requests de API y acciones POST
    if (url.pathname.startsWith('/api/') ||
        url.pathname.includes('/Like') ||
        url.pathname.includes('/Follow') ||
        url.pathname.includes('/Mensaje')) {
        return;
    }

    // Ignorar Range requests (causan respuestas 206 que no se pueden cachear)
    if (request.headers.get('Range')) {
        return;
    }

    // Ignorar videos - causan error 206 (Partial Content) que no se puede cachear
    if (isVideo(url.pathname)) {
        return;
    }

    // Estrategia segun tipo de recurso
    if (isStaticAsset(url.pathname)) {
        // Cache First para recursos estaticos
        event.respondWith(cacheFirst(request));
    } else if (isImageUpload(url.pathname)) {
        // Cache First para uploads (imagenes de usuarios)
        event.respondWith(cacheFirst(request));
    } else {
        // Network First para paginas HTML y contenido dinamico
        event.respondWith(networkFirst(request));
    }
});

// Detectar si es un recurso estatico
function isStaticAsset(pathname) {
    return pathname.match(/\.(css|js|woff2?|ttf|eot|ico|png|jpg|jpeg|gif|svg|webp)$/i) ||
           pathname.startsWith('/lib/') ||
           pathname.startsWith('/css/') ||
           pathname.startsWith('/js/') ||
           pathname.startsWith('/images/icons/');
}

// Detectar si es un upload de usuario (solo imagenes, no videos)
function isImageUpload(pathname) {
    return pathname.startsWith('/uploads/') &&
           !pathname.match(/\.(mp4|webm|mov|avi|mkv|m4v|3gp)$/i);
}

// Detectar si es un video (no cachear)
function isVideo(pathname) {
    return pathname.match(/\.(mp4|webm|mov|avi|mkv|m4v|3gp)$/i);
}

// Estrategia: Cache First (para recursos estaticos)
async function cacheFirst(request) {
    try {
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
            // Actualizar cache en background (stale-while-revalidate)
            updateCache(request);
            return cachedResponse;
        }

        const networkResponse = await fetch(request);
        // Solo cachear respuestas completas (status 200), no parciales (206)
        if (networkResponse.ok && networkResponse.status === 200) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone());
        }
        return networkResponse;
    } catch (error) {
        console.warn('[SW] Cache First fallo:', error);
        // Intentar devolver algo del cache
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
            return cachedResponse;
        }
        // Para imagenes, devolver placeholder
        if (request.destination === 'image') {
            return caches.match('/images/placeholder.png');
        }
        throw error;
    }
}

// Estrategia: Network First (para paginas HTML)
async function networkFirst(request) {
    try {
        const networkResponse = await fetch(request);
        // Solo cachear respuestas completas (status 200), no parciales (206)
        if (networkResponse.ok && networkResponse.status === 200) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone());
        }
        return networkResponse;
    } catch (error) {
        console.warn('[SW] Network First fallo, buscando en cache:', error);

        // Intentar obtener del cache
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
            return cachedResponse;
        }

        // Si es una pagina HTML, mostrar pagina offline
        if (request.destination === 'document' ||
            request.headers.get('accept')?.includes('text/html')) {
            const offlinePage = await caches.match(OFFLINE_URL);
            if (offlinePage) {
                return offlinePage;
            }
        }

        throw error;
    }
}

// Actualizar cache en background
async function updateCache(request) {
    try {
        const networkResponse = await fetch(request);
        // Solo cachear respuestas completas (status 200), no parciales (206)
        if (networkResponse.ok && networkResponse.status === 200) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, networkResponse.clone());
        }
    } catch (error) {
        // Silenciar errores de actualizacion en background
    }
}

// Escuchar mensajes del cliente
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }

    if (event.data && event.data.type === 'CLEAR_CACHE') {
        caches.delete(CACHE_NAME).then(() => {
            console.log('[SW] Cache limpiado');
        });
    }
});

// Push notifications (para futuro uso)
self.addEventListener('push', (event) => {
    if (!event.data) return;

    try {
        const data = event.data.json();
        const options = {
            body: data.body || 'Nueva notificacion',
            icon: '/images/icons/icon-192x192.png',
            badge: '/images/icons/icon-72x72.png',
            vibrate: [100, 50, 100],
            data: {
                url: data.url || '/'
            },
            actions: data.actions || []
        };

        event.waitUntil(
            self.registration.showNotification(data.title || 'LADO', options)
        );
    } catch (error) {
        console.error('[SW] Error procesando push:', error);
    }
});

// Click en notificacion
self.addEventListener('notificationclick', (event) => {
    event.notification.close();

    const url = event.notification.data?.url || '/';
    const action = event.action;

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then((clientList) => {
                // Manejar acciones específicas
                if (action === 'view') {
                    // Acción ver - navegar a la URL
                } else if (action === 'dismiss') {
                    // Solo cerrar
                    return;
                }

                // Si ya hay una ventana abierta, enfocarla
                for (const client of clientList) {
                    if (client.url.includes(self.location.origin) && 'focus' in client) {
                        client.navigate(url);
                        return client.focus();
                    }
                }
                // Si no, abrir nueva ventana
                if (clients.openWindow) {
                    return clients.openWindow(url);
                }
            })
    );
});

// ========================================
// BACKGROUND SYNC - Para acciones offline
// ========================================
self.addEventListener('sync', (event) => {
    console.log('[SW] Background Sync:', event.tag);

    if (event.tag === 'sync-likes') {
        event.waitUntil(syncLikes());
    } else if (event.tag === 'sync-comments') {
        event.waitUntil(syncComments());
    } else if (event.tag === 'sync-follows') {
        event.waitUntil(syncFollows());
    }
});

// Sincronizar likes pendientes
async function syncLikes() {
    try {
        const db = await openIndexedDB();
        const pendingLikes = await db.getAll('pending-likes');

        for (const like of pendingLikes) {
            try {
                const response = await fetch('/Feed/Like', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-CSRF-TOKEN': like.token
                    },
                    body: JSON.stringify({ contenidoId: like.contenidoId })
                });

                if (response.ok) {
                    await db.delete('pending-likes', like.id);
                }
            } catch (error) {
                console.warn('[SW] Error sincronizando like:', error);
            }
        }
    } catch (error) {
        console.warn('[SW] Error en syncLikes:', error);
    }
}

// Sincronizar comentarios pendientes
async function syncComments() {
    try {
        const db = await openIndexedDB();
        const pendingComments = await db.getAll('pending-comments');

        for (const comment of pendingComments) {
            try {
                const response = await fetch('/Feed/Comentar', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-CSRF-TOKEN': comment.token
                    },
                    body: JSON.stringify({
                        contenidoId: comment.contenidoId,
                        texto: comment.texto
                    })
                });

                if (response.ok) {
                    await db.delete('pending-comments', comment.id);
                    // Notificar al cliente
                    const clients = await self.clients.matchAll();
                    clients.forEach(client => {
                        client.postMessage({
                            type: 'COMMENT_SYNCED',
                            contenidoId: comment.contenidoId
                        });
                    });
                }
            } catch (error) {
                console.warn('[SW] Error sincronizando comentario:', error);
            }
        }
    } catch (error) {
        console.warn('[SW] Error en syncComments:', error);
    }
}

// Sincronizar follows pendientes
async function syncFollows() {
    try {
        const db = await openIndexedDB();
        const pendingFollows = await db.getAll('pending-follows');

        for (const follow of pendingFollows) {
            try {
                const response = await fetch('/Feed/Seguir', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-CSRF-TOKEN': follow.token
                    },
                    body: JSON.stringify({ creadorId: follow.creadorId })
                });

                if (response.ok) {
                    await db.delete('pending-follows', follow.id);
                }
            } catch (error) {
                console.warn('[SW] Error sincronizando follow:', error);
            }
        }
    } catch (error) {
        console.warn('[SW] Error en syncFollows:', error);
    }
}

// Helper para IndexedDB
function openIndexedDB() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open('lado-offline', 1);

        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            const db = request.result;
            resolve({
                getAll: (store) => new Promise((res, rej) => {
                    const tx = db.transaction(store, 'readonly');
                    const req = tx.objectStore(store).getAll();
                    req.onsuccess = () => res(req.result);
                    req.onerror = () => rej(req.error);
                }),
                delete: (store, key) => new Promise((res, rej) => {
                    const tx = db.transaction(store, 'readwrite');
                    const req = tx.objectStore(store).delete(key);
                    req.onsuccess = () => res();
                    req.onerror = () => rej(req.error);
                })
            });
        };

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
}

// ========================================
// PERIODIC BACKGROUND SYNC
// ========================================
self.addEventListener('periodicsync', (event) => {
    if (event.tag === 'check-notifications') {
        event.waitUntil(checkNewNotifications());
    }
});

async function checkNewNotifications() {
    try {
        const response = await fetch('/api/Notificaciones/NuevasCount');
        if (response.ok) {
            const data = await response.json();
            if (data.count > 0) {
                self.registration.showNotification('Lado', {
                    body: `Tienes ${data.count} nuevas notificaciones`,
                    icon: '/images/icons/icon-192x192.png',
                    badge: '/images/icons/icon-72x72.png',
                    tag: 'new-notifications',
                    data: { url: '/Usuario/Notificaciones' }
                });
            }
        }
    } catch (error) {
        console.warn('[SW] Error checking notifications:', error);
    }
}

// ========================================
// LIMPIEZA DE CACHE
// ========================================
async function cleanupCaches() {
    // Limpiar cache de imágenes si excede el límite
    const imageCache = await caches.open(IMAGE_CACHE);
    const imageKeys = await imageCache.keys();

    if (imageKeys.length > IMAGE_CACHE_MAX_ITEMS) {
        // Eliminar las más antiguas (primeras en la lista)
        const toDelete = imageKeys.slice(0, imageKeys.length - IMAGE_CACHE_MAX_ITEMS);
        for (const request of toDelete) {
            await imageCache.delete(request);
        }
        console.log(`[SW] Limpiadas ${toDelete.length} imágenes del cache`);
    }
}
