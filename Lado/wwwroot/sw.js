// Service Worker para LADO PWA
const CACHE_NAME = 'lado-cache-v3';
const OFFLINE_URL = '/offline.html';

// Recursos para pre-cachear (shell de la app)
const PRECACHE_ASSETS = [
    '/',
    '/offline.html',
    '/css/site.css',
    '/js/site.js',
    '/manifest.json',
    '/images/angelito.png',
    '/images/diablito.png',
    // Fuentes de Google (si las usas)
    'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap'
];

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

    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames
                        .filter((name) => name !== CACHE_NAME)
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

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then((clientList) => {
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
