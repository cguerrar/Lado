/**
 * AudioMixer.js - Mezclador de Audio
 * Biblioteca de música y control de volumen con Web Audio API
 */

class AudioMixer {
    constructor() {
        this.panel = null;
        this.isVisible = false;

        // Audio context y nodos
        this.audioContext = null;
        this.musicSource = null;
        this.musicGainNode = null;
        this.originalGainNode = null;

        // Estado
        this.selectedTrack = null;
        this.musicAudio = null;
        this.previewAudio = null;
        this.isPlaying = false;
        this.musicVolume = 0.7;
        this.originalVolume = 1.0;

        // Biblioteca de música
        this.tracks = [];
        this.trendingTracks = [];
        this.genres = [];
        this.currentGenre = 'todos';
        this.currentTab = 'todos'; // 'todos', 'trending'
        this.searchQuery = '';

        // Tiempos de recorte de audio
        this.audioStartTime = 0;
        this.audioDuration = 0;

        this.init();
    }

    init() {
        this.createPanel();
        this.loadLibrary();
    }

    async loadLibrary() {
        try {
            // Cargar en paralelo: géneros, pistas y trending
            const [generosResponse, pistasResponse, trendingResponse] = await Promise.all([
                fetch('/api/Musica/generos'),
                fetch('/api/Musica/biblioteca'),
                fetch('/api/Musica/trending')
            ]);

            if (generosResponse.ok) {
                this.genres = await generosResponse.json();
            }

            if (pistasResponse.ok) {
                this.tracks = await pistasResponse.json();
            }

            if (trendingResponse.ok) {
                this.trendingTracks = await trendingResponse.json();
            }

            this.renderTabs();
            this.renderGenres();
            this.renderTracks();
        } catch (error) {
            console.error('Error loading music library:', error);
            this.showMessage('Error al cargar la biblioteca de música', 'error');
        }
    }

    createPanel() {
        this.panel = document.createElement('div');
        this.panel.className = 'audio-mixer-panel';
        this.panel.innerHTML = `
            <div class="audio-panel-header">
                <span class="audio-panel-title">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                        <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                    </svg>
                    Música
                </span>
                <button class="audio-panel-close" id="closeAudioPanel">&times;</button>
            </div>

            <div class="audio-panel-content">
                <!-- Search Bar Mejorado -->
                <div class="audio-search-container">
                    <div class="audio-search-bar">
                        <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="11" cy="11" r="8"></circle>
                            <path d="M21 21l-4.35-4.35"></path>
                        </svg>
                        <input type="text" id="musicSearchInput" placeholder="Buscar por título, artista...">
                        <button class="search-clear-btn" id="searchClearBtn" style="display: none;">
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
                            </svg>
                        </button>
                    </div>
                    <div class="search-results-count" id="searchResultsCount"></div>
                </div>

                <!-- Tabs: Todos / Más usados -->
                <div class="audio-tabs" id="audioTabs">
                    <button class="audio-tab active" data-tab="todos">
                        <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                            <path d="M4 6h16v2H4zm0 5h16v2H4zm0 5h16v2H4z"/>
                        </svg>
                        Todos
                    </button>
                    <button class="audio-tab" data-tab="trending">
                        <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                            <path d="M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z"/>
                        </svg>
                        Más usados
                        <span class="tab-badge" id="trendingCount">0</span>
                    </button>
                </div>

                <!-- Genre Filter (solo visible en tab Todos) -->
                <div class="genre-filter" id="genreFilter">
                    <button class="genre-btn active" data-genre="todos">Todos</button>
                    <!-- Genres will be rendered here -->
                </div>

                <!-- Track List -->
                <div class="track-list" id="trackList">
                    <div class="track-loading">
                        <div class="spinner-small"></div>
                        <span>Cargando música...</span>
                    </div>
                </div>

                <!-- Selected Track Info -->
                <div class="selected-track-info" id="selectedTrackInfo" style="display: none;">
                    <div class="selected-track-header">
                        <span class="selected-label">Seleccionada:</span>
                        <button class="remove-track-btn" id="removeTrackBtn" title="Quitar música">
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
                            </svg>
                        </button>
                    </div>
                    <div class="selected-track-details">
                        <img id="selectedTrackCover" src="" alt="">
                        <div class="selected-track-text">
                            <span class="selected-track-title" id="selectedTrackTitle">-</span>
                            <span class="selected-track-artist" id="selectedTrackArtist">-</span>
                        </div>
                    </div>
                </div>

                <!-- Volume Controls -->
                <div class="volume-controls" id="volumeControls">
                    <div class="volume-group">
                        <label>
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"/>
                            </svg>
                            Música
                        </label>
                        <input type="range" id="musicVolumeSlider" min="0" max="100" value="70" class="volume-slider">
                        <span class="volume-value" id="musicVolumeValue">70%</span>
                    </div>
                    <div class="volume-group">
                        <label>
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M12 14c1.66 0 2.99-1.34 2.99-3L15 5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm5.3-3c0 3-2.54 5.1-5.3 5.1S6.7 14 6.7 11H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c3.28-.48 6-3.3 6-6.72h-1.7z"/>
                            </svg>
                            Audio Original
                        </label>
                        <input type="range" id="originalVolumeSlider" min="0" max="100" value="100" class="volume-slider">
                        <span class="volume-value" id="originalVolumeValue">100%</span>
                    </div>
                </div>

                <!-- Audio Trim Section -->
                <div class="audio-trim-section" id="audioTrimSection" style="display: none;">
                    <label>Recorte de Audio <span class="trim-hint">(arrastra los bordes)</span></label>
                    <div class="audio-waveform" id="audioWaveform">
                        <div class="audio-trim-region" id="audioTrimRegion">
                            <div class="trim-handle trim-handle-left" id="trimHandleLeft"></div>
                            <div class="trim-handle trim-handle-right" id="trimHandleRight"></div>
                        </div>
                        <div class="audio-playhead" id="audioPlayhead"></div>
                    </div>
                    <div class="audio-trim-times">
                        <span id="audioTrimStart">0:00</span>
                        <span id="audioTrimDuration">0:00</span>
                        <span id="audioTrimEnd">0:00</span>
                    </div>
                </div>

                <!-- Preview Button -->
                <div class="audio-preview-controls">
                    <button class="audio-preview-btn" id="audioPreviewBtn">
                        <svg viewBox="0 0 24 24" fill="currentColor" id="previewPlayIcon">
                            <polygon points="5,3 19,12 5,21"/>
                        </svg>
                        <svg viewBox="0 0 24 24" fill="currentColor" id="previewPauseIcon" style="display: none;">
                            <rect x="6" y="4" width="4" height="16"/>
                            <rect x="14" y="4" width="4" height="16"/>
                        </svg>
                        <span id="previewBtnText">Previsualizar</span>
                    </button>
                </div>

                <!-- Use Music Button -->
                <div class="audio-action-buttons" id="audioActionButtons" style="display: none;">
                    <button class="use-music-btn" id="useMusicBtn">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
                        </svg>
                        Usar esta música
                    </button>
                </div>
            </div>
        `;

        this.bindPanelEvents();
    }

    bindPanelEvents() {
        // Close panel
        this.panel.querySelector('#closeAudioPanel').addEventListener('click', () => this.hide());

        // Search
        const searchInput = this.panel.querySelector('#musicSearchInput');
        const clearBtn = this.panel.querySelector('#searchClearBtn');
        let searchTimeout;

        searchInput.addEventListener('input', () => {
            clearTimeout(searchTimeout);
            const value = searchInput.value;

            // Mostrar/ocultar botón de limpiar
            clearBtn.style.display = value ? 'flex' : 'none';

            searchTimeout = setTimeout(() => {
                this.searchQuery = value;
                this.filterTracks();
                this.updateSearchResultsCount();
            }, 300);
        });

        // Focus en el buscador
        searchInput.addEventListener('focus', () => {
            searchInput.parentElement.classList.add('focused');
        });

        searchInput.addEventListener('blur', () => {
            searchInput.parentElement.classList.remove('focused');
        });

        // Clear search
        clearBtn.addEventListener('click', () => {
            searchInput.value = '';
            clearBtn.style.display = 'none';
            this.searchQuery = '';
            this.filterTracks();
            this.updateSearchResultsCount();
            searchInput.focus();
        });

        // Volume sliders
        const musicSlider = this.panel.querySelector('#musicVolumeSlider');
        musicSlider.addEventListener('input', () => {
            this.musicVolume = parseInt(musicSlider.value) / 100;
            this.panel.querySelector('#musicVolumeValue').textContent = `${musicSlider.value}%`;
            this.updateMusicVolume();
        });

        const originalSlider = this.panel.querySelector('#originalVolumeSlider');
        originalSlider.addEventListener('input', () => {
            this.originalVolume = parseInt(originalSlider.value) / 100;
            this.panel.querySelector('#originalVolumeValue').textContent = `${originalSlider.value}%`;
            this.updateOriginalVolume();
        });

        // Remove track
        this.panel.querySelector('#removeTrackBtn').addEventListener('click', () => this.removeTrack());

        // Preview button
        this.panel.querySelector('#audioPreviewBtn').addEventListener('click', () => this.togglePreview());

        // Use music button
        this.panel.querySelector('#useMusicBtn').addEventListener('click', () => this.confirmMusicSelection());

        // Initialize trim controls
        this.initTrimControls();
    }

    initTrimControls() {
        const waveform = this.panel.querySelector('#audioWaveform');
        const trimRegion = this.panel.querySelector('#audioTrimRegion');
        const leftHandle = this.panel.querySelector('#trimHandleLeft');
        const rightHandle = this.panel.querySelector('#trimHandleRight');

        let isDragging = null; // 'left', 'right', or 'region'
        let startX = 0;
        let startLeft = 0;
        let startRight = 0;

        const getPercent = (clientX) => {
            const rect = waveform.getBoundingClientRect();
            return Math.max(0, Math.min(100, ((clientX - rect.left) / rect.width) * 100));
        };

        const updateTrimFromPercent = (leftPercent, rightPercent) => {
            if (!this.selectedTrack) return;

            const trackDuration = this.selectedTrack.duracion || 180; // default 3 min
            this.audioStartTime = (leftPercent / 100) * trackDuration;
            const endTime = (rightPercent / 100) * trackDuration;
            this.audioDuration = endTime - this.audioStartTime;

            // Min duration 5 seconds
            if (this.audioDuration < 5) {
                this.audioDuration = 5;
            }

            // Update visual
            trimRegion.style.left = `${leftPercent}%`;
            trimRegion.style.right = `${100 - rightPercent}%`;

            this.updateTrimDisplay();
        };

        // Left handle drag
        const onMouseDown = (e, type) => {
            e.preventDefault();
            e.stopPropagation();
            isDragging = type;
            startX = e.clientX || e.touches?.[0]?.clientX;
            startLeft = parseFloat(trimRegion.style.left) || 10;
            startRight = 100 - (parseFloat(trimRegion.style.right) || 10);

            document.addEventListener('mousemove', onMouseMove);
            document.addEventListener('mouseup', onMouseUp);
            document.addEventListener('touchmove', onMouseMove, { passive: false });
            document.addEventListener('touchend', onMouseUp);
        };

        const onMouseMove = (e) => {
            if (!isDragging) return;
            e.preventDefault();

            const clientX = e.clientX || e.touches?.[0]?.clientX;
            const percent = getPercent(clientX);

            if (isDragging === 'left') {
                const maxLeft = startRight - 5; // min 5% width
                const newLeft = Math.min(percent, maxLeft);
                updateTrimFromPercent(newLeft, startRight);
            } else if (isDragging === 'right') {
                const minRight = startLeft + 5; // min 5% width
                const newRight = Math.max(percent, minRight);
                updateTrimFromPercent(startLeft, newRight);
            }
        };

        const onMouseUp = () => {
            isDragging = null;
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
            document.removeEventListener('touchmove', onMouseMove);
            document.removeEventListener('touchend', onMouseUp);
        };

        // Attach events
        leftHandle.addEventListener('mousedown', (e) => onMouseDown(e, 'left'));
        leftHandle.addEventListener('touchstart', (e) => onMouseDown(e, 'left'), { passive: false });
        rightHandle.addEventListener('mousedown', (e) => onMouseDown(e, 'right'));
        rightHandle.addEventListener('touchstart', (e) => onMouseDown(e, 'right'), { passive: false });

        // Click on waveform to set position
        waveform.addEventListener('click', (e) => {
            if (e.target === leftHandle || e.target === rightHandle) return;
            const percent = getPercent(e.clientX);
            const currentLeft = parseFloat(trimRegion.style.left) || 10;
            const currentRight = 100 - (parseFloat(trimRegion.style.right) || 10);

            // Move closest handle
            const distToLeft = Math.abs(percent - currentLeft);
            const distToRight = Math.abs(percent - currentRight);

            if (distToLeft < distToRight) {
                updateTrimFromPercent(Math.min(percent, currentRight - 5), currentRight);
            } else {
                updateTrimFromPercent(currentLeft, Math.max(percent, currentLeft + 5));
            }
        });
    }

    renderTabs() {
        const tabsContainer = this.panel.querySelector('#audioTabs');
        const trendingCountBadge = this.panel.querySelector('#trendingCount');

        // Actualizar contador de trending
        if (trendingCountBadge) {
            trendingCountBadge.textContent = this.trendingTracks.length;
        }

        // Bind tab buttons
        tabsContainer.querySelectorAll('.audio-tab').forEach(btn => {
            btn.addEventListener('click', () => {
                tabsContainer.querySelectorAll('.audio-tab').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.currentTab = btn.dataset.tab;

                // Mostrar/ocultar filtro de géneros según tab
                const genreFilter = this.panel.querySelector('#genreFilter');
                genreFilter.style.display = this.currentTab === 'todos' ? 'flex' : 'none';

                this.filterTracks();
            });
        });
    }

    renderGenres() {
        const container = this.panel.querySelector('#genreFilter');

        // Keep the "Todos" button
        let html = '<button class="genre-btn active" data-genre="todos">Todos</button>';

        this.genres.forEach(genre => {
            // Soportar tanto camelCase (nombre) como PascalCase (Nombre)
            const genreName = genre.nombre || genre.Nombre || 'Sin género';
            html += `<button class="genre-btn" data-genre="${genreName}">${genreName}</button>`;
        });

        container.innerHTML = html;

        // Bind genre buttons
        container.querySelectorAll('.genre-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                container.querySelectorAll('.genre-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.currentGenre = btn.dataset.genre;
                this.filterTracks();
            });
        });
    }

    updateSearchResultsCount() {
        const container = this.panel.querySelector('#searchResultsCount');
        if (!this.searchQuery) {
            container.textContent = '';
            container.style.display = 'none';
            return;
        }

        const filtered = this.getFilteredTracks();
        container.textContent = `${filtered.length} resultado${filtered.length !== 1 ? 's' : ''}`;
        container.style.display = 'block';
    }

    renderTracks(tracks = null) {
        const container = this.panel.querySelector('#trackList');
        const tracksToRender = tracks || this.getFilteredTracks();
        const isTrending = this.currentTab === 'trending';

        if (tracksToRender.length === 0) {
            const message = this.searchQuery
                ? `No se encontraron resultados para "${this.searchQuery}"`
                : 'No se encontraron pistas';

            container.innerHTML = `
                <div class="no-tracks">
                    <svg viewBox="0 0 24 24" fill="currentColor">
                        <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                    </svg>
                    <span>${message}</span>
                </div>
            `;
            return;
        }

        container.innerHTML = tracksToRender.map((track, index) => `
            <div class="track-item ${this.selectedTrack?.id === track.id ? 'selected' : ''} ${isTrending ? 'trending-item' : ''}"
                 data-track-id="${track.id}">
                ${isTrending ? `<div class="track-rank">${index + 1}</div>` : ''}
                <div class="track-cover">
                    <img src="${track.rutaPortada || '/images/music-placeholder.svg'}" alt="${track.titulo}">
                    <button class="track-play-btn" data-track-id="${track.id}">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <polygon points="5,3 19,12 5,21"/>
                        </svg>
                    </button>
                </div>
                <div class="track-info">
                    <span class="track-title">${track.titulo}</span>
                    <span class="track-artist">${track.artista}</span>
                    <div class="track-meta">
                        <span class="track-duration">${track.duracionFormateada}</span>
                        <span class="track-genre">${track.genero}</span>
                        ${isTrending && track.contadorUsos ? `<span class="track-uses">${track.contadorUsos} usos</span>` : ''}
                    </div>
                </div>
                <button class="track-select-btn" data-track-id="${track.id}" title="Usar esta pista">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="20 6 9 17 4 12"></polyline>
                    </svg>
                </button>
            </div>
        `).join('');

        // Bind track events
        container.querySelectorAll('.track-play-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const trackId = parseInt(btn.dataset.trackId);
                this.previewTrack(trackId);
            });
        });

        container.querySelectorAll('.track-select-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const trackId = parseInt(btn.dataset.trackId);
                this.selectTrack(trackId);
            });
        });

        container.querySelectorAll('.track-item').forEach(item => {
            item.addEventListener('click', () => {
                const trackId = parseInt(item.dataset.trackId);
                this.selectTrack(trackId);
            });
        });
    }

    filterTracks() {
        const filtered = this.getFilteredTracks();
        this.renderTracks(filtered);
    }

    getFilteredTracks() {
        // Seleccionar la lista base según la pestaña
        let filtered = this.currentTab === 'trending' ? [...this.trendingTracks] : [...this.tracks];

        // Filter by genre (solo en pestaña Todos)
        if (this.currentTab === 'todos' && this.currentGenre !== 'todos') {
            filtered = filtered.filter(t => t.genero === this.currentGenre);
        }

        // Filter by search
        if (this.searchQuery) {
            const query = this.searchQuery.toLowerCase();
            filtered = filtered.filter(t =>
                t.titulo.toLowerCase().includes(query) ||
                t.artista.toLowerCase().includes(query) ||
                (t.estadoAnimo && t.estadoAnimo.toLowerCase().includes(query))
            );
        }

        return filtered;
    }

    async previewTrack(trackId) {
        // Buscar en ambas listas
        const track = this.tracks.find(t => t.id === trackId) || this.trendingTracks.find(t => t.id === trackId);
        if (!track) {
            console.warn('Track not found:', trackId);
            return;
        }

        console.log('Previewing track:', track.titulo, 'Path:', track.rutaArchivo);

        // Stop current preview if playing
        if (this.previewAudio) {
            this.previewAudio.pause();
            this.previewAudio = null;
        }

        try {
            if (!track.rutaArchivo) {
                this.showMessage('Esta pista no tiene archivo de audio', 'error');
                return;
            }

            this.previewAudio = new Audio(track.rutaArchivo);
            this.previewAudio.volume = this.musicVolume;

            this.previewAudio.addEventListener('ended', () => {
                this.stopPreview();
            });

            this.previewAudio.addEventListener('error', (e) => {
                console.error('Audio error:', e);
                this.showMessage('Error al cargar el archivo de audio', 'error');
            });

            await this.previewAudio.play();
            this.showMessage(`Reproduciendo: ${track.titulo}`, 'success');

            // Auto-stop after 15 seconds
            setTimeout(() => {
                if (this.previewAudio) {
                    this.stopPreview();
                }
            }, 15000);

        } catch (error) {
            console.error('Error previewing track:', error);
            this.showMessage('Error al reproducir la pista: ' + error.message, 'error');
        }
    }

    stopPreview() {
        if (this.previewAudio) {
            this.previewAudio.pause();
            this.previewAudio = null;
        }
    }

    selectTrack(trackId) {
        // Buscar en ambas listas
        const track = this.tracks.find(t => t.id === trackId) || this.trendingTracks.find(t => t.id === trackId);
        if (!track) return;

        this.selectedTrack = track;
        this.stopPreview();

        // Update UI
        this.panel.querySelector('#selectedTrackInfo').style.display = 'block';
        this.panel.querySelector('#selectedTrackTitle').textContent = track.titulo;
        this.panel.querySelector('#selectedTrackArtist').textContent = track.artista;
        this.panel.querySelector('#selectedTrackCover').src = track.rutaPortada || '/images/music-placeholder.svg';

        // Show trim section and action buttons
        this.panel.querySelector('#audioTrimSection').style.display = 'block';
        this.panel.querySelector('#audioActionButtons').style.display = 'block';
        this.audioDuration = track.duracion;
        this.audioStartTime = 0;
        this.updateTrimDisplay();

        // Update track list selection
        this.renderTracks(this.getFilteredTracks());

        // Initialize audio for mixing
        this.initializeAudio(track);

        // Notificar al padre que se seleccionó una pista
        if (this.onTrackChange) {
            this.onTrackChange(track);
        }
    }

    removeTrack() {
        this.selectedTrack = null;
        this.stopPreview();

        if (this.musicAudio) {
            this.musicAudio.pause();
            this.musicAudio = null;
        }

        this.panel.querySelector('#selectedTrackInfo').style.display = 'none';
        this.panel.querySelector('#audioTrimSection').style.display = 'none';
        this.panel.querySelector('#audioActionButtons').style.display = 'none';

        this.renderTracks(this.getFilteredTracks());
        this.showMessage('Música eliminada', 'info');

        // Notificar al padre que se eliminó la pista
        if (this.onTrackChange) {
            this.onTrackChange(null);
        }

        // Notificar que se quitó la música confirmada
        if (this.onMusicConfirmed) {
            this.onMusicConfirmed(null);
        }
    }

    initializeAudio(track) {
        console.log('Initializing audio for track:', track.titulo, 'Path:', track.rutaArchivo);

        // Create new audio element
        if (this.musicAudio) {
            this.musicAudio.pause();
        }

        if (!track.rutaArchivo) {
            console.error('Track has no audio file path');
            this.showMessage('Esta pista no tiene archivo de audio', 'error');
            return;
        }

        this.musicAudio = new Audio(track.rutaArchivo);
        this.musicAudio.volume = this.musicVolume;
        this.musicAudio.loop = true;

        // Add error handler
        this.musicAudio.addEventListener('error', (e) => {
            console.error('Music audio error:', e, this.musicAudio.error);
            this.showMessage('Error al cargar el archivo de audio', 'error');
        });

        this.musicAudio.addEventListener('canplaythrough', () => {
            console.log('Audio ready to play');
        });

        // Initialize Web Audio API if needed
        if (!this.audioContext) {
            try {
                this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
            } catch (e) {
                console.warn('Web Audio API not supported');
            }
        }
    }

    updateMusicVolume() {
        if (this.musicAudio) {
            this.musicAudio.volume = this.musicVolume;
        }
        if (this.previewAudio) {
            this.previewAudio.volume = this.musicVolume;
        }
    }

    updateOriginalVolume() {
        // This will be connected to the video element in ReelsCreator
        if (this.onOriginalVolumeChange) {
            this.onOriginalVolumeChange(this.originalVolume);
        }
    }

    updateTrimDisplay() {
        const startMin = Math.floor(this.audioStartTime / 60);
        const startSec = Math.floor(this.audioStartTime % 60);
        const endTime = this.audioStartTime + this.audioDuration;
        const endMin = Math.floor(endTime / 60);
        const endSec = Math.floor(endTime % 60);
        const durMin = Math.floor(this.audioDuration / 60);
        const durSec = Math.floor(this.audioDuration % 60);

        this.panel.querySelector('#audioTrimStart').textContent = `${startMin}:${startSec.toString().padStart(2, '0')}`;
        this.panel.querySelector('#audioTrimEnd').textContent = `${endMin}:${endSec.toString().padStart(2, '0')}`;
        this.panel.querySelector('#audioTrimDuration').textContent = `${durMin}:${durSec.toString().padStart(2, '0')}`;
    }

    togglePreview() {
        if (!this.selectedTrack) {
            this.showMessage('Selecciona una pista primero', 'warning');
            return;
        }

        if (this.isPlaying) {
            this.pausePreview();
        } else {
            this.playPreview();
        }
    }

    async playPreview() {
        if (!this.musicAudio) {
            console.warn('No music audio loaded');
            this.showMessage('Selecciona una pista primero', 'warning');
            return;
        }

        try {
            console.log('Playing preview, start time:', this.audioStartTime);
            this.musicAudio.currentTime = this.audioStartTime;
            await this.musicAudio.play();
            this.isPlaying = true;

            this.panel.querySelector('#previewPlayIcon').style.display = 'none';
            this.panel.querySelector('#previewPauseIcon').style.display = 'block';
            this.panel.querySelector('#previewBtnText').textContent = 'Pausar';

            // Notify parent to sync video playback
            if (this.onPlayStateChange) {
                this.onPlayStateChange(true);
            }
        } catch (error) {
            console.error('Error playing preview:', error);
            this.showMessage('Error al reproducir: ' + error.message, 'error');
        }
    }

    pausePreview() {
        if (this.musicAudio) {
            this.musicAudio.pause();
        }
        this.isPlaying = false;

        this.panel.querySelector('#previewPlayIcon').style.display = 'block';
        this.panel.querySelector('#previewPauseIcon').style.display = 'none';
        this.panel.querySelector('#previewBtnText').textContent = 'Previsualizar';

        if (this.onPlayStateChange) {
            this.onPlayStateChange(false);
        }
    }

    // Sync with video playback
    syncWithVideo(videoElement) {
        if (!videoElement) return;

        // Update original volume based on slider
        videoElement.volume = this.originalVolume;

        // Sync play/pause
        videoElement.addEventListener('play', () => {
            if (this.musicAudio && this.selectedTrack) {
                this.musicAudio.currentTime = this.audioStartTime;
                this.musicAudio.play();
            }
        });

        videoElement.addEventListener('pause', () => {
            if (this.musicAudio) {
                this.musicAudio.pause();
            }
        });

        videoElement.addEventListener('seeked', () => {
            if (this.musicAudio && this.selectedTrack) {
                // Sync music position with video
                const videoTime = videoElement.currentTime;
                this.musicAudio.currentTime = this.audioStartTime + (videoTime % this.audioDuration);
            }
        });

        // Store callback for volume changes
        this.onOriginalVolumeChange = (volume) => {
            videoElement.volume = volume;
        };
    }

    // Get audio data for export
    getAudioData() {
        if (!this.selectedTrack) {
            return null;
        }

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

    // Confirm music selection and notify parent
    confirmMusicSelection() {
        if (!this.selectedTrack) {
            this.showMessage('Selecciona una pista primero', 'warning');
            return;
        }

        const audioData = this.getAudioData();

        // Notificar al padre que se confirmó la música
        if (this.onMusicConfirmed) {
            this.onMusicConfirmed(audioData);
        }

        this.showMessage(`Música "${this.selectedTrack.titulo}" añadida`, 'success');

        // Cerrar el panel
        this.hide();
    }

    show(container) {
        // Agregar al body para que position:fixed funcione correctamente
        if (!this.panel.parentElement || this.panel.parentElement !== document.body) {
            document.body.appendChild(this.panel);
        }
        this.panel.classList.add('visible');
        this.isVisible = true;
    }

    hide() {
        this.panel.classList.remove('visible');
        this.isVisible = false;
        this.stopPreview();
        this.pausePreview();
    }

    toggle(container) {
        if (this.isVisible) {
            this.hide();
        } else {
            this.show(container);
        }
    }

    reset() {
        this.selectedTrack = null;
        this.stopPreview();
        this.pausePreview();

        if (this.musicAudio) {
            this.musicAudio.pause();
            this.musicAudio = null;
        }

        this.musicVolume = 0.7;
        this.originalVolume = 1.0;
        this.audioStartTime = 0;

        // Reset UI
        if (this.panel) {
            this.panel.querySelector('#selectedTrackInfo').style.display = 'none';
            this.panel.querySelector('#audioTrimSection').style.display = 'none';
            this.panel.querySelector('#audioActionButtons').style.display = 'none';
            this.panel.querySelector('#musicVolumeSlider').value = 70;
            this.panel.querySelector('#originalVolumeSlider').value = 100;
            this.panel.querySelector('#musicVolumeValue').textContent = '70%';
            this.panel.querySelector('#originalVolumeValue').textContent = '100%';
            this.panel.querySelector('#musicSearchInput').value = '';
        }

        this.searchQuery = '';
        this.currentGenre = 'todos';
    }

    showMessage(text, type = 'info') {
        // Use the global toast if available
        if (typeof showToast === 'function') {
            showToast(type === 'error' ? 'Error' : type === 'success' ? 'Éxito' : 'Info', text, type);
        } else {
            console.log(`[${type}] ${text}`);
        }
    }

    destroy() {
        this.reset();
        if (this.audioContext) {
            this.audioContext.close();
        }
        this.panel.remove();
    }
}
