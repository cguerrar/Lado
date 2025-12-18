/**
 * AudioMixer.js - Selector de Música Minimalista
 * Drawer estilo Spotify con cards y preview integrado
 */

class AudioMixer {
    constructor() {
        this.drawer = null;
        this.overlay = null;
        this.isVisible = false;

        // Estado
        this.selectedTrack = null;
        this.expandedTrackId = null;
        this.previewAudio = null;
        this.isPlaying = false;
        this.musicVolume = 0.7;
        this.originalVolume = 1.0;

        // Tiempos de recorte
        this.audioStartTime = 0;
        this.audioDuration = 15; // Default 15 segundos

        // Biblioteca
        this.tracks = [];
        this.trendingTracks = [];
        this.searchQuery = '';
        this.isLoading = true;

        // Long press para preview
        this.longPressTimer = null;
        this.longPressDelay = 400;

        this.init();
    }

    init() {
        this.createDrawer();
        this.loadLibrary();
    }

    async loadLibrary() {
        try {
            this.isLoading = true;
            this.renderTracks();

            const [pistasResponse, trendingResponse] = await Promise.all([
                fetch('/api/Musica/biblioteca'),
                fetch('/api/Musica/trending')
            ]);

            if (pistasResponse.ok) {
                this.tracks = await pistasResponse.json();
            }

            if (trendingResponse.ok) {
                this.trendingTracks = await trendingResponse.json();
            }

            this.isLoading = false;
            this.renderTracks();
        } catch (error) {
            console.error('Error loading music library:', error);
            this.isLoading = false;
            this.renderTracks();
        }
    }

    createDrawer() {
        // Overlay
        this.overlay = document.createElement('div');
        this.overlay.className = 'music-drawer-overlay';
        this.overlay.addEventListener('click', () => this.hide());

        // Drawer
        this.drawer = document.createElement('div');
        this.drawer.className = 'music-drawer';
        this.drawer.innerHTML = `
            <div class="drawer-handle" id="drawerHandle">
                <div class="handle-bar"></div>
            </div>

            <div class="drawer-header">
                <h3>
                    <svg viewBox="0 0 24 24" fill="currentColor" width="22" height="22">
                        <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                    </svg>
                    Añadir música
                </h3>
                <button class="drawer-close" id="closeDrawer">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
                        <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
                    </svg>
                </button>
            </div>

            <div class="drawer-search">
                <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="11" cy="11" r="8"></circle>
                    <path d="M21 21l-4.35-4.35"></path>
                </svg>
                <input type="text" id="musicSearch" placeholder="Buscar canciones...">
            </div>

            <div class="drawer-content">
                <div class="music-section" id="trendingSection">
                    <h4>🔥 Populares</h4>
                    <div class="music-cards-row" id="trendingCards"></div>
                </div>

                <div class="music-section">
                    <h4>📚 Biblioteca</h4>
                    <div class="music-list" id="musicList"></div>
                </div>
            </div>

            <!-- Card expandida / Mini player -->
            <div class="expanded-player" id="expandedPlayer">
                <div class="expanded-player-content">
                    <img class="expanded-cover" id="expandedCover" src="" alt="">
                    <div class="expanded-info">
                        <span class="expanded-title" id="expandedTitle">-</span>
                        <span class="expanded-artist" id="expandedArtist">-</span>
                    </div>
                    <button class="expanded-play" id="expandedPlayBtn">
                        <svg viewBox="0 0 24 24" fill="currentColor" id="playIcon">
                            <polygon points="5,3 19,12 5,21"/>
                        </svg>
                        <svg viewBox="0 0 24 24" fill="currentColor" id="pauseIcon" style="display:none;">
                            <rect x="6" y="4" width="4" height="16"/>
                            <rect x="14" y="4" width="4" height="16"/>
                        </svg>
                    </button>
                </div>

                <div class="expanded-trim">
                    <div class="trim-slider-container">
                        <input type="range" class="trim-slider" id="trimSlider" min="0" max="100" value="0">
                        <div class="trim-time">
                            <span id="trimStart">0:00</span>
                            <span id="trimDuration">15s</span>
                        </div>
                    </div>
                </div>

                <div class="expanded-volume">
                    <div class="volume-row">
                        <svg viewBox="0 0 24 24" fill="currentColor" width="18" height="18">
                            <path d="M3 9v6h4l5 5V4L7 9H3z"/>
                        </svg>
                        <input type="range" class="mini-volume" id="musicVolume" min="0" max="100" value="70">
                        <span id="musicVolText">70%</span>
                    </div>
                </div>

                <button class="use-track-btn" id="useTrackBtn">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                        <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
                    </svg>
                    Usar esta canción
                </button>
            </div>
        `;

        // Estilos
        this.injectStyles();
        this.bindEvents();
    }

    injectStyles() {
        if (document.getElementById('music-drawer-styles')) return;

        const styles = document.createElement('style');
        styles.id = 'music-drawer-styles';
        styles.textContent = `
            /* Overlay */
            .music-drawer-overlay {
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0, 0, 0, 0.5);
                backdrop-filter: blur(4px);
                z-index: 9998;
                opacity: 0;
                visibility: hidden;
                transition: all 0.3s ease;
            }
            .music-drawer-overlay.visible {
                opacity: 1;
                visibility: visible;
            }

            /* Drawer - Modal flotante arrastrable */
            .music-drawer {
                position: fixed;
                left: 50%;
                top: 50%;
                transform: translate(-50%, -50%) scale(0.9);
                width: 90%;
                max-width: 400px;
                max-height: 70vh;
                background: #fff;
                border-radius: 16px;
                z-index: 9999;
                opacity: 0;
                visibility: hidden;
                transition: opacity 0.25s ease, transform 0.25s ease, visibility 0.25s;
                display: flex;
                flex-direction: column;
                box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                overflow: hidden;
            }
            .music-drawer.visible {
                opacity: 1;
                visibility: visible;
                transform: translate(-50%, -50%) scale(1);
            }
            .music-drawer.dragging {
                transition: none;
                cursor: grabbing;
            }
            .music-drawer.has-selection {
                max-height: 80vh;
            }

            /* Handle - ahora es la barra de título arrastrable */
            .drawer-handle {
                padding: 10px 16px;
                display: flex;
                justify-content: center;
                cursor: grab;
                background: linear-gradient(135deg, #4682B4, #36648B);
                border-radius: 16px 16px 0 0;
            }
            .drawer-handle:active {
                cursor: grabbing;
            }
            .handle-bar {
                width: 40px;
                height: 4px;
                background: rgba(255,255,255,0.5);
                border-radius: 2px;
            }

            /* Header */
            .drawer-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                padding: 0 20px 16px;
            }
            .drawer-header h3 {
                display: flex;
                align-items: center;
                gap: 10px;
                font-size: 1.25rem;
                font-weight: 700;
                color: #111;
                margin: 0;
            }
            .drawer-header h3 svg {
                color: #4682B4;
            }
            .drawer-close {
                width: 36px;
                height: 36px;
                border: none;
                background: #f5f5f5;
                border-radius: 50%;
                cursor: pointer;
                display: flex;
                align-items: center;
                justify-content: center;
                color: #666;
                transition: all 0.2s;
            }
            .drawer-close:hover {
                background: #eee;
                color: #333;
            }

            /* Search */
            .drawer-search {
                margin: 0 20px 16px;
                position: relative;
            }
            .drawer-search .search-icon {
                position: absolute;
                left: 14px;
                top: 50%;
                transform: translateY(-50%);
                width: 18px;
                height: 18px;
                color: #999;
            }
            .drawer-search input {
                width: 100%;
                padding: 12px 16px 12px 44px;
                border: none;
                background: #f5f5f5;
                border-radius: 12px;
                font-size: 15px;
                color: #333;
                outline: none;
                transition: all 0.2s;
            }
            .drawer-search input:focus {
                background: #f0f0f0;
                box-shadow: 0 0 0 2px rgba(70, 130, 180, 0.2);
            }
            .drawer-search input::placeholder {
                color: #999;
            }

            /* Content */
            .drawer-content {
                flex: 1;
                overflow-y: auto;
                padding-bottom: 20px;
                min-height: 0;
            }
            .music-drawer.has-selection .drawer-content {
                padding-bottom: 10px;
            }

            /* Sections */
            .music-section {
                padding: 0 20px;
                margin-bottom: 24px;
            }
            .music-section h4 {
                font-size: 14px;
                font-weight: 600;
                color: #666;
                margin: 0 0 12px 0;
                text-transform: uppercase;
                letter-spacing: 0.5px;
            }

            /* Trending cards - horizontal scroll */
            .music-cards-row {
                display: flex;
                gap: 12px;
                overflow-x: auto;
                padding-bottom: 8px;
                scrollbar-width: none;
                -ms-overflow-style: none;
            }
            .music-cards-row::-webkit-scrollbar {
                display: none;
            }

            /* Music card (trending) */
            .music-card {
                flex-shrink: 0;
                width: 140px;
                background: #f8f8f8;
                border-radius: 12px;
                padding: 12px;
                cursor: pointer;
                transition: all 0.2s;
                border: 2px solid transparent;
                position: relative;
                overflow: hidden;
            }
            .music-card:hover {
                background: #f0f0f0;
                transform: translateY(-2px);
            }
            .music-card.selected {
                border-color: #4682B4;
                background: rgba(70, 130, 180, 0.08);
            }
            .music-card.playing::before {
                content: '';
                position: absolute;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(70, 130, 180, 0.1);
                animation: pulse 1.5s infinite;
            }
            @keyframes pulse {
                0%, 100% { opacity: 0.5; }
                50% { opacity: 1; }
            }
            .music-card-cover {
                width: 100%;
                aspect-ratio: 1;
                border-radius: 8px;
                overflow: hidden;
                margin-bottom: 10px;
                position: relative;
                background: linear-gradient(135deg, #4682B4, #36648B);
            }
            .music-card-cover img {
                width: 100%;
                height: 100%;
                object-fit: cover;
            }
            .music-card-cover .play-overlay {
                position: absolute;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0,0,0,0.3);
                display: flex;
                align-items: center;
                justify-content: center;
                opacity: 0;
                transition: opacity 0.2s;
            }
            .music-card:hover .play-overlay,
            .music-card.playing .play-overlay {
                opacity: 1;
            }
            .play-overlay svg {
                width: 32px;
                height: 32px;
                color: white;
            }
            .music-card-title {
                font-size: 13px;
                font-weight: 600;
                color: #111;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
                margin-bottom: 2px;
            }
            .music-card-artist {
                font-size: 12px;
                color: #666;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }

            /* Music list (biblioteca) */
            .music-list {
                display: flex;
                flex-direction: column;
                gap: 4px;
            }

            /* Music item (lista) */
            .music-item {
                display: flex;
                align-items: center;
                gap: 12px;
                padding: 10px 12px;
                border-radius: 12px;
                cursor: pointer;
                transition: all 0.15s;
                border: 2px solid transparent;
            }
            .music-item:hover {
                background: #f5f5f5;
            }
            .music-item.selected {
                background: rgba(70, 130, 180, 0.08);
                border-color: #4682B4;
            }
            .music-item.playing {
                background: rgba(70, 130, 180, 0.12);
            }
            .music-item-cover {
                width: 48px;
                height: 48px;
                border-radius: 8px;
                overflow: hidden;
                flex-shrink: 0;
                position: relative;
                background: linear-gradient(135deg, #4682B4, #36648B);
            }
            .music-item-cover img {
                width: 100%;
                height: 100%;
                object-fit: cover;
            }
            .music-item-cover .play-mini {
                position: absolute;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0,0,0,0.4);
                display: flex;
                align-items: center;
                justify-content: center;
                opacity: 0;
                transition: opacity 0.15s;
            }
            .music-item:hover .play-mini,
            .music-item.playing .play-mini {
                opacity: 1;
            }
            .play-mini svg {
                width: 20px;
                height: 20px;
                color: white;
            }
            .music-item-info {
                flex: 1;
                min-width: 0;
            }
            .music-item-title {
                font-size: 14px;
                font-weight: 600;
                color: #111;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }
            .music-item-meta {
                font-size: 12px;
                color: #888;
                display: flex;
                gap: 8px;
                margin-top: 2px;
            }
            .music-item-check {
                width: 24px;
                height: 24px;
                border-radius: 50%;
                border: 2px solid #ddd;
                display: flex;
                align-items: center;
                justify-content: center;
                flex-shrink: 0;
                transition: all 0.15s;
            }
            .music-item-check svg {
                width: 14px;
                height: 14px;
                color: white;
                opacity: 0;
            }
            .music-item.selected .music-item-check {
                background: #4682B4;
                border-color: #4682B4;
            }
            .music-item.selected .music-item-check svg {
                opacity: 1;
            }

            /* Expanded player */
            .expanded-player {
                background: #f8f8f8;
                border-top: 1px solid #eee;
                padding: 16px 20px;
                display: none;
                flex-shrink: 0;
            }
            .expanded-player.visible {
                display: block;
            }

            .expanded-player-content {
                display: flex;
                align-items: center;
                gap: 12px;
                margin-bottom: 12px;
            }
            .expanded-cover {
                width: 56px;
                height: 56px;
                border-radius: 10px;
                object-fit: cover;
                background: linear-gradient(135deg, #4682B4, #36648B);
            }
            .expanded-info {
                flex: 1;
                min-width: 0;
            }
            .expanded-title {
                display: block;
                font-size: 15px;
                font-weight: 700;
                color: #111;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }
            .expanded-artist {
                display: block;
                font-size: 13px;
                color: #666;
                margin-top: 2px;
            }
            .expanded-play {
                width: 44px;
                height: 44px;
                border-radius: 50%;
                border: none;
                background: #4682B4;
                color: white;
                cursor: pointer;
                display: flex;
                align-items: center;
                justify-content: center;
                transition: all 0.2s;
                flex-shrink: 0;
            }
            .expanded-play:hover {
                background: #36648B;
                transform: scale(1.05);
            }
            .expanded-play svg {
                width: 20px;
                height: 20px;
            }

            /* Trim slider */
            .expanded-trim {
                margin-bottom: 12px;
            }
            .trim-slider-container {
                position: relative;
            }
            .trim-slider {
                width: 100%;
                height: 6px;
                -webkit-appearance: none;
                appearance: none;
                background: #e5e5e5;
                border-radius: 3px;
                outline: none;
            }
            .trim-slider::-webkit-slider-thumb {
                -webkit-appearance: none;
                width: 18px;
                height: 18px;
                background: #4682B4;
                border-radius: 50%;
                cursor: pointer;
                box-shadow: 0 2px 6px rgba(70, 130, 180, 0.3);
            }
            .trim-slider::-moz-range-thumb {
                width: 18px;
                height: 18px;
                background: #4682B4;
                border-radius: 50%;
                cursor: pointer;
                border: none;
            }
            .trim-time {
                display: flex;
                justify-content: space-between;
                margin-top: 6px;
                font-size: 12px;
                color: #888;
            }

            /* Volume */
            .expanded-volume {
                margin-bottom: 12px;
            }
            .volume-row {
                display: flex;
                align-items: center;
                gap: 10px;
                color: #666;
            }
            .mini-volume {
                flex: 1;
                height: 4px;
                -webkit-appearance: none;
                appearance: none;
                background: #e5e5e5;
                border-radius: 2px;
                outline: none;
            }
            .mini-volume::-webkit-slider-thumb {
                -webkit-appearance: none;
                width: 14px;
                height: 14px;
                background: #4682B4;
                border-radius: 50%;
                cursor: pointer;
            }
            .mini-volume::-moz-range-thumb {
                width: 14px;
                height: 14px;
                background: #4682B4;
                border-radius: 50%;
                cursor: pointer;
                border: none;
            }
            #musicVolText {
                font-size: 12px;
                color: #888;
                min-width: 36px;
                text-align: right;
            }

            /* Use track button */
            .use-track-btn {
                width: 100%;
                padding: 12px;
                background: linear-gradient(135deg, #4682B4, #36648B);
                color: white;
                border: none;
                border-radius: 10px;
                font-size: 14px;
                font-weight: 600;
                cursor: pointer;
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 8px;
                transition: all 0.2s;
            }
            .use-track-btn:hover {
                transform: translateY(-1px);
                box-shadow: 0 4px 15px rgba(70, 130, 180, 0.35);
            }
            .use-track-btn:active {
                transform: translateY(0);
            }

            /* Loading & Empty states */
            .music-loading, .music-empty {
                text-align: center;
                padding: 40px 20px;
                color: #999;
            }
            .music-loading svg, .music-empty svg {
                width: 48px;
                height: 48px;
                margin-bottom: 12px;
                opacity: 0.5;
            }
            .spinner-ring {
                animation: spin 1s linear infinite;
            }
            @keyframes spin {
                to { transform: rotate(360deg); }
            }

            /* Responsive */
            @media (max-width: 480px) {
                .music-drawer {
                    width: 95%;
                    max-height: 75vh;
                }
                .music-drawer.has-selection {
                    max-height: 85vh;
                }
            }

            /* Small screens */
            @media (max-height: 600px) {
                .music-drawer {
                    max-height: 85vh;
                }
                .music-drawer.has-selection {
                    max-height: 90vh;
                }
                .expanded-cover {
                    width: 44px;
                    height: 44px;
                }
                .expanded-player {
                    padding: 10px 14px;
                }
                .expanded-trim, .expanded-volume {
                    margin-bottom: 6px;
                }
                .music-card {
                    width: 110px;
                }
            }
        `;
        document.head.appendChild(styles);
    }

    bindEvents() {
        // Close drawer
        this.drawer.querySelector('#closeDrawer').addEventListener('click', () => this.hide());

        // Search
        const searchInput = this.drawer.querySelector('#musicSearch');
        let searchTimeout;
        searchInput.addEventListener('input', () => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                this.searchQuery = searchInput.value.toLowerCase();
                this.renderTracks();
            }, 200);
        });

        // Expanded player controls
        this.drawer.querySelector('#expandedPlayBtn').addEventListener('click', () => this.togglePreview());

        // Volume slider
        const volumeSlider = this.drawer.querySelector('#musicVolume');
        volumeSlider.addEventListener('input', () => {
            this.musicVolume = parseInt(volumeSlider.value) / 100;
            this.drawer.querySelector('#musicVolText').textContent = `${volumeSlider.value}%`;
            if (this.previewAudio) {
                this.previewAudio.volume = this.musicVolume;
            }
        });

        // Trim slider
        const trimSlider = this.drawer.querySelector('#trimSlider');
        trimSlider.addEventListener('input', () => {
            if (!this.selectedTrack) return;
            const duration = this.selectedTrack.duracion || 180;
            const maxStart = Math.max(0, duration - 15);
            this.audioStartTime = (parseInt(trimSlider.value) / 100) * maxStart;
            this.updateTrimDisplay();
        });

        // Use track button
        this.drawer.querySelector('#useTrackBtn').addEventListener('click', () => this.confirmSelection());

        // Drag to move
        this.initDragGesture();
    }

    initDragGesture() {
        const handle = this.drawer.querySelector('#drawerHandle');
        let isDragging = false;
        let startX = 0;
        let startY = 0;
        let initialLeft = 0;
        let initialTop = 0;

        const onStart = (e) => {
            isDragging = true;
            this.drawer.classList.add('dragging');

            const clientX = e.touches ? e.touches[0].clientX : e.clientX;
            const clientY = e.touches ? e.touches[0].clientY : e.clientY;

            startX = clientX;
            startY = clientY;

            // Obtener posición actual del drawer
            const rect = this.drawer.getBoundingClientRect();
            initialLeft = rect.left + rect.width / 2;
            initialTop = rect.top + rect.height / 2;

            e.preventDefault();
        };

        const onMove = (e) => {
            if (!isDragging) return;

            const clientX = e.touches ? e.touches[0].clientX : e.clientX;
            const clientY = e.touches ? e.touches[0].clientY : e.clientY;

            const deltaX = clientX - startX;
            const deltaY = clientY - startY;

            const newLeft = initialLeft + deltaX;
            const newTop = initialTop + deltaY;

            // Limitar a los bordes de la pantalla
            const rect = this.drawer.getBoundingClientRect();
            const minX = rect.width / 2;
            const maxX = window.innerWidth - rect.width / 2;
            const minY = rect.height / 2;
            const maxY = window.innerHeight - rect.height / 2;

            const clampedLeft = Math.max(minX, Math.min(maxX, newLeft));
            const clampedTop = Math.max(minY, Math.min(maxY, newTop));

            this.drawer.style.left = `${clampedLeft}px`;
            this.drawer.style.top = `${clampedTop}px`;
            this.drawer.style.transform = 'translate(-50%, -50%) scale(1)';
        };

        const onEnd = () => {
            if (!isDragging) return;
            isDragging = false;
            this.drawer.classList.remove('dragging');
        };

        handle.addEventListener('touchstart', onStart, { passive: false });
        document.addEventListener('touchmove', onMove, { passive: true });
        document.addEventListener('touchend', onEnd);
        handle.addEventListener('mousedown', onStart);
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onEnd);
    }

    renderTracks() {
        const trendingContainer = this.drawer.querySelector('#trendingCards');
        const listContainer = this.drawer.querySelector('#musicList');
        const trendingSection = this.drawer.querySelector('#trendingSection');

        // Loading state
        if (this.isLoading) {
            listContainer.innerHTML = `
                <div class="music-loading">
                    <svg class="spinner-ring" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10" opacity="0.25"/>
                        <path d="M12 2a10 10 0 0 1 10 10" stroke-linecap="round"/>
                    </svg>
                    <p>Cargando música...</p>
                </div>
            `;
            return;
        }

        // Filter tracks
        let filteredTracks = this.tracks;
        let filteredTrending = this.trendingTracks;

        if (this.searchQuery) {
            const q = this.searchQuery;
            filteredTracks = this.tracks.filter(t =>
                t.titulo.toLowerCase().includes(q) ||
                t.artista.toLowerCase().includes(q)
            );
            filteredTrending = this.trendingTracks.filter(t =>
                t.titulo.toLowerCase().includes(q) ||
                t.artista.toLowerCase().includes(q)
            );
        }

        // Render trending cards
        if (filteredTrending.length > 0 && !this.searchQuery) {
            trendingSection.style.display = 'block';
            trendingContainer.innerHTML = filteredTrending.slice(0, 10).map(track => `
                <div class="music-card ${this.selectedTrack?.id === track.id ? 'selected' : ''}"
                     data-track-id="${track.id}">
                    <div class="music-card-cover">
                        <img src="${track.rutaPortada || '/images/music-placeholder.svg'}" alt="">
                        <div class="play-overlay">
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <polygon points="5,3 19,12 5,21"/>
                            </svg>
                        </div>
                    </div>
                    <div class="music-card-title">${track.titulo}</div>
                    <div class="music-card-artist">${track.artista}</div>
                </div>
            `).join('');

            // Bind events
            trendingContainer.querySelectorAll('.music-card').forEach(card => {
                this.bindTrackEvents(card, parseInt(card.dataset.trackId));
            });
        } else {
            trendingSection.style.display = 'none';
        }

        // Render list
        if (filteredTracks.length === 0) {
            listContainer.innerHTML = `
                <div class="music-empty">
                    <svg viewBox="0 0 24 24" fill="currentColor">
                        <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                    </svg>
                    <p>${this.searchQuery ? 'No se encontraron resultados' : 'No hay música disponible'}</p>
                </div>
            `;
            return;
        }

        listContainer.innerHTML = filteredTracks.map(track => `
            <div class="music-item ${this.selectedTrack?.id === track.id ? 'selected' : ''}"
                 data-track-id="${track.id}">
                <div class="music-item-cover">
                    <img src="${track.rutaPortada || '/images/music-placeholder.svg'}" alt="">
                    <div class="play-mini">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <polygon points="5,3 19,12 5,21"/>
                        </svg>
                    </div>
                </div>
                <div class="music-item-info">
                    <div class="music-item-title">${track.titulo}</div>
                    <div class="music-item-meta">
                        <span>${track.artista}</span>
                        <span>•</span>
                        <span>${track.duracionFormateada || this.formatDuration(track.duracion)}</span>
                    </div>
                </div>
                <div class="music-item-check">
                    <svg viewBox="0 0 24 24" fill="currentColor">
                        <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
                    </svg>
                </div>
            </div>
        `).join('');

        // Bind events
        listContainer.querySelectorAll('.music-item').forEach(item => {
            this.bindTrackEvents(item, parseInt(item.dataset.trackId));
        });
    }

    bindTrackEvents(element, trackId) {
        // Click to select
        element.addEventListener('click', () => this.selectTrack(trackId));

        // Long press to preview
        element.addEventListener('touchstart', (e) => {
            this.longPressTimer = setTimeout(() => {
                this.previewTrack(trackId);
                element.classList.add('playing');
            }, this.longPressDelay);
        }, { passive: true });

        element.addEventListener('touchend', () => {
            clearTimeout(this.longPressTimer);
            if (this.previewAudio) {
                this.stopPreview();
                element.classList.remove('playing');
            }
        });

        element.addEventListener('touchmove', () => {
            clearTimeout(this.longPressTimer);
        }, { passive: true });

        // Mouse hold for desktop
        element.addEventListener('mousedown', () => {
            this.longPressTimer = setTimeout(() => {
                this.previewTrack(trackId);
                element.classList.add('playing');
            }, this.longPressDelay);
        });

        element.addEventListener('mouseup', () => {
            clearTimeout(this.longPressTimer);
            this.stopPreview();
            element.classList.remove('playing');
        });

        element.addEventListener('mouseleave', () => {
            clearTimeout(this.longPressTimer);
            this.stopPreview();
            element.classList.remove('playing');
        });
    }

    selectTrack(trackId) {
        const track = this.tracks.find(t => t.id === trackId) ||
                      this.trendingTracks.find(t => t.id === trackId);
        if (!track) return;

        this.selectedTrack = track;
        this.audioStartTime = 0;
        this.audioDuration = Math.min(15, track.duracion || 15);

        // Update UI
        this.renderTracks();
        this.showExpandedPlayer(track);

        // Notify parent
        if (this.onTrackChange) {
            this.onTrackChange(track);
        }
    }

    showExpandedPlayer(track) {
        const player = this.drawer.querySelector('#expandedPlayer');

        this.drawer.querySelector('#expandedCover').src = track.rutaPortada || '/images/music-placeholder.svg';
        this.drawer.querySelector('#expandedTitle').textContent = track.titulo;
        this.drawer.querySelector('#expandedArtist').textContent = track.artista;

        // Reset trim slider
        this.drawer.querySelector('#trimSlider').value = 0;
        this.updateTrimDisplay();

        player.classList.add('visible');
        this.drawer.classList.add('has-selection');
    }

    hideExpandedPlayer() {
        this.drawer.querySelector('#expandedPlayer').classList.remove('visible');
        this.drawer.classList.remove('has-selection');
    }

    updateTrimDisplay() {
        const start = this.audioStartTime;
        this.drawer.querySelector('#trimStart').textContent = this.formatTime(start);
        this.drawer.querySelector('#trimDuration').textContent = `${Math.round(this.audioDuration)}s`;
    }

    async previewTrack(trackId) {
        const track = this.tracks.find(t => t.id === trackId) ||
                      this.trendingTracks.find(t => t.id === trackId);
        if (!track || !track.rutaArchivo) return;

        this.stopPreview();

        try {
            this.previewAudio = new Audio(track.rutaArchivo);
            this.previewAudio.volume = this.musicVolume;
            await this.previewAudio.play();
        } catch (error) {
            console.error('Error playing preview:', error);
        }
    }

    stopPreview() {
        if (this.previewAudio) {
            this.previewAudio.pause();
            this.previewAudio = null;
        }
        this.isPlaying = false;
        this.updatePlayButton();
    }

    togglePreview() {
        if (!this.selectedTrack) return;

        if (this.isPlaying) {
            this.stopPreview();
        } else {
            this.playSelectedTrack();
        }
    }

    async playSelectedTrack() {
        if (!this.selectedTrack || !this.selectedTrack.rutaArchivo) return;

        this.stopPreview();

        try {
            this.previewAudio = new Audio(this.selectedTrack.rutaArchivo);
            this.previewAudio.volume = this.musicVolume;
            this.previewAudio.currentTime = this.audioStartTime;

            this.previewAudio.addEventListener('ended', () => this.stopPreview());

            await this.previewAudio.play();
            this.isPlaying = true;
            this.updatePlayButton();

            // Auto stop after duration
            setTimeout(() => {
                if (this.isPlaying) this.stopPreview();
            }, this.audioDuration * 1000);
        } catch (error) {
            console.error('Error playing track:', error);
        }
    }

    updatePlayButton() {
        const playIcon = this.drawer.querySelector('#playIcon');
        const pauseIcon = this.drawer.querySelector('#pauseIcon');

        if (this.isPlaying) {
            playIcon.style.display = 'none';
            pauseIcon.style.display = 'block';
        } else {
            playIcon.style.display = 'block';
            pauseIcon.style.display = 'none';
        }
    }

    confirmSelection() {
        if (!this.selectedTrack) return;

        const audioData = {
            trackId: this.selectedTrack.id,
            trackTitle: this.selectedTrack.titulo,
            trackArtist: this.selectedTrack.artista,
            rutaArchivo: this.selectedTrack.rutaArchivo,
            startTime: this.audioStartTime,
            duration: this.audioDuration,
            musicVolume: this.musicVolume,
            originalVolume: this.originalVolume
        };

        if (this.onMusicConfirmed) {
            this.onMusicConfirmed(audioData);
        }

        this.hide();
    }

    formatDuration(seconds) {
        if (!seconds) return '0:00';
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }

    formatTime(seconds) {
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }

    show() {
        if (!this.overlay.parentElement) {
            document.body.appendChild(this.overlay);
            document.body.appendChild(this.drawer);
        }

        requestAnimationFrame(() => {
            this.overlay.classList.add('visible');
            this.drawer.classList.add('visible');
        });

        this.isVisible = true;
        document.body.style.overflow = 'hidden';
    }

    hide() {
        this.overlay.classList.remove('visible');
        this.drawer.classList.remove('visible');
        this.hideExpandedPlayer();
        this.stopPreview();
        this.isVisible = false;
        document.body.style.overflow = '';

        // Resetear posición al centro para próxima apertura
        setTimeout(() => {
            this.drawer.style.left = '50%';
            this.drawer.style.top = '50%';
            this.drawer.style.transform = 'translate(-50%, -50%) scale(0.9)';
        }, 300);
    }

    toggle() {
        if (this.isVisible) {
            this.hide();
        } else {
            this.show();
        }
    }

    // API compatible con versión anterior
    getAudioData() {
        if (!this.selectedTrack) return null;

        return {
            trackId: this.selectedTrack.id,
            trackTitle: this.selectedTrack.titulo,
            trackArtist: this.selectedTrack.artista,
            rutaArchivo: this.selectedTrack.rutaArchivo,
            startTime: this.audioStartTime,
            duration: this.audioDuration,
            musicVolume: this.musicVolume,
            originalVolume: this.originalVolume
        };
    }

    reset() {
        this.selectedTrack = null;
        this.audioStartTime = 0;
        this.audioDuration = 15;
        this.musicVolume = 0.7;
        this.stopPreview();
        this.hideExpandedPlayer();
        this.renderTracks();
    }

    showToast(message) {
        if (typeof showToast === 'function') {
            showToast('Música', message, 'success');
        }
    }

    destroy() {
        this.reset();
        this.overlay?.remove();
        this.drawer?.remove();
    }
}
