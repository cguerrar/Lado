/**
 * DrawingCanvas.js - Herramienta de Dibujo
 * Canvas transparente para dibujar a mano alzada
 */

class DrawingCanvas {
    constructor(container) {
        this.container = container;
        this.canvas = null;
        this.ctx = null;
        this.panel = null;
        this.isVisible = false;
        this.isDrawing = false;
        this.isEnabled = false;

        // Configuración de dibujo
        this.brushColor = '#ffffff';
        this.brushSize = 5;
        this.tool = 'brush'; // 'brush' | 'eraser'
        this.opacity = 1;

        // Historial para deshacer/rehacer
        this.history = [];
        this.historyIndex = -1;
        this.maxHistory = 50;

        // Último punto para líneas suaves
        this.lastX = 0;
        this.lastY = 0;

        // Colores predefinidos
        this.colors = [
            '#ffffff', '#000000', '#ff0000', '#ff6b6b',
            '#ffa500', '#ffeb3b', '#4caf50', '#00bcd4',
            '#2196f3', '#3f51b5', '#9c27b0', '#e91e63',
            '#795548', '#607d8b', '#00ff00', '#ff00ff'
        ];

        // Tamaños de pincel
        this.brushSizes = [2, 5, 10, 15, 20, 30];

        // Referencia al handler de teclado para poder removerlo
        this.keyboardHandler = null;

        this.init();
    }

    init() {
        this.createCanvas();
        this.createPanel();
    }

    createCanvas() {
        this.canvas = document.createElement('canvas');
        this.canvas.className = 'drawing-canvas';
        this.canvas.style.cssText = `
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            pointer-events: none;
            z-index: 15;
            touch-action: none;
        `;
        this.ctx = this.canvas.getContext('2d');
    }

    createPanel() {
        this.panel = document.createElement('div');
        this.panel.className = 'drawing-panel';
        this.panel.innerHTML = `
            <div class="drawing-panel-header">
                <span class="drawing-panel-title">Dibujar</span>
                <button class="drawing-panel-close" id="closeDrawingPanel">&times;</button>
            </div>

            <div class="drawing-panel-content">
                <!-- Tool Selection -->
                <div class="drawing-option-group">
                    <label>Herramienta</label>
                    <div class="tool-buttons">
                        <button class="tool-btn active" data-tool="brush" title="Pincel">
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M7 14c-1.66 0-3 1.34-3 3 0 1.31-1.16 2-2 2 .92 1.22 2.49 2 4 2 2.21 0 4-1.79 4-4 0-1.66-1.34-3-3-3zm13.71-9.37l-1.34-1.34a.996.996 0 0 0-1.41 0L9 12.25 11.75 15l8.96-8.96a.996.996 0 0 0 0-1.41z"/>
                            </svg>
                        </button>
                        <button class="tool-btn" data-tool="eraser" title="Borrador">
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M16.24 3.56l4.95 4.94c.78.79.78 2.05 0 2.84L12 20.53a4.008 4.008 0 0 1-5.66 0L2.81 17c-.78-.79-.78-2.05 0-2.84l10.6-10.6c.79-.78 2.05-.78 2.83 0zm-1.41 1.42L6.93 12.9l4.24 4.24 7.9-7.9-4.24-4.26z"/>
                            </svg>
                        </button>
                    </div>
                </div>

                <!-- Brush Size -->
                <div class="drawing-option-group">
                    <label>Grosor: <span id="brushSizeValue">${this.brushSize}</span>px</label>
                    <div class="brush-size-selector">
                        ${this.brushSizes.map(size => `
                            <button class="size-btn ${size === this.brushSize ? 'active' : ''}"
                                    data-size="${size}"
                                    title="${size}px">
                                <span class="size-preview" style="width: ${Math.min(size, 20)}px; height: ${Math.min(size, 20)}px;"></span>
                            </button>
                        `).join('')}
                    </div>
                    <input type="range" id="brushSizeSlider" min="1" max="50" value="${this.brushSize}" class="drawing-slider">
                </div>

                <!-- Color Palette -->
                <div class="drawing-option-group">
                    <label>Color</label>
                    <div class="color-palette drawing-colors" id="drawingColorPalette">
                        ${this.colors.map(color => `
                            <button class="color-btn ${color === this.brushColor ? 'active' : ''}"
                                    data-color="${color}"
                                    style="background-color: ${color}">
                            </button>
                        `).join('')}
                    </div>
                    <div class="custom-color-row">
                        <label>Personalizado:</label>
                        <input type="color" id="customColorPicker" value="${this.brushColor}" class="custom-color-picker">
                    </div>
                </div>

                <!-- Opacity -->
                <div class="drawing-option-group">
                    <label>Opacidad: <span id="opacityValue">100</span>%</label>
                    <input type="range" id="opacitySlider" min="10" max="100" value="100" class="drawing-slider">
                </div>

                <!-- Actions -->
                <div class="drawing-actions">
                    <button class="drawing-action-btn" id="undoBtn" title="Deshacer (Ctrl+Z)">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z"/>
                        </svg>
                        Deshacer
                    </button>
                    <button class="drawing-action-btn" id="redoBtn" title="Rehacer (Ctrl+Y)">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M18.4 10.6C16.55 8.99 14.15 8 11.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16c1.05-3.19 4.05-5.5 7.6-5.5 1.95 0 3.73.72 5.12 1.88L13 16h9V7l-3.6 3.6z"/>
                        </svg>
                        Rehacer
                    </button>
                </div>

                <div class="drawing-actions">
                    <button class="drawing-action-btn danger" id="clearCanvasBtn" title="Borrar todo">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/>
                        </svg>
                        Borrar Todo
                    </button>
                </div>

                <!-- Toggle Drawing Mode -->
                <div class="drawing-toggle-section">
                    <button class="drawing-mode-btn" id="toggleDrawingMode">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.996.996 0 0 0 0-1.41l-2.34-2.34a.996.996 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/>
                        </svg>
                        <span id="drawingModeText">Activar Dibujo</span>
                    </button>
                    <p class="drawing-hint">Activa el modo dibujo para empezar a pintar sobre la imagen</p>
                </div>
            </div>
        `;

        this.bindPanelEvents();
    }

    bindPanelEvents() {
        // Close panel
        this.panel.querySelector('#closeDrawingPanel').addEventListener('click', () => this.hide());

        // Tool selection
        this.panel.querySelectorAll('.tool-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.tool-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.tool = btn.dataset.tool;
                this.updateCursor();
            });
        });

        // Brush size buttons
        this.panel.querySelectorAll('.size-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.size-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.brushSize = parseInt(btn.dataset.size);
                this.panel.querySelector('#brushSizeSlider').value = this.brushSize;
                this.panel.querySelector('#brushSizeValue').textContent = this.brushSize;
            });
        });

        // Brush size slider
        const sizeSlider = this.panel.querySelector('#brushSizeSlider');
        sizeSlider.addEventListener('input', () => {
            this.brushSize = parseInt(sizeSlider.value);
            this.panel.querySelector('#brushSizeValue').textContent = this.brushSize;
            // Update active size button
            this.panel.querySelectorAll('.size-btn').forEach(b => {
                b.classList.toggle('active', parseInt(b.dataset.size) === this.brushSize);
            });
        });

        // Color palette
        this.panel.querySelectorAll('#drawingColorPalette .color-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('#drawingColorPalette .color-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.brushColor = btn.dataset.color;
                this.panel.querySelector('#customColorPicker').value = this.brushColor;
            });
        });

        // Custom color picker
        this.panel.querySelector('#customColorPicker').addEventListener('input', (e) => {
            this.brushColor = e.target.value;
            this.panel.querySelectorAll('#drawingColorPalette .color-btn').forEach(b => b.classList.remove('active'));
        });

        // Opacity slider
        const opacitySlider = this.panel.querySelector('#opacitySlider');
        opacitySlider.addEventListener('input', () => {
            this.opacity = parseInt(opacitySlider.value) / 100;
            this.panel.querySelector('#opacityValue').textContent = opacitySlider.value;
        });

        // Undo/Redo
        this.panel.querySelector('#undoBtn').addEventListener('click', () => this.undo());
        this.panel.querySelector('#redoBtn').addEventListener('click', () => this.redo());

        // Clear canvas
        this.panel.querySelector('#clearCanvasBtn').addEventListener('click', () => this.clearCanvas());

        // Toggle drawing mode
        this.panel.querySelector('#toggleDrawingMode').addEventListener('click', () => this.toggleDrawingMode());

        // Keyboard shortcuts - guardar referencia para poder remover
        this.keyboardHandler = (e) => {
            if (!this.isVisible) return;

            if (e.ctrlKey && e.key === 'z') {
                e.preventDefault();
                this.undo();
            } else if (e.ctrlKey && e.key === 'y') {
                e.preventDefault();
                this.redo();
            }
        };
        document.addEventListener('keydown', this.keyboardHandler);
    }

    bindCanvasEvents() {
        // Mouse events
        this.canvas.addEventListener('mousedown', (e) => this.startDrawing(e));
        this.canvas.addEventListener('mousemove', (e) => this.draw(e));
        this.canvas.addEventListener('mouseup', () => this.stopDrawing());
        this.canvas.addEventListener('mouseout', () => this.stopDrawing());

        // Touch events
        this.canvas.addEventListener('touchstart', (e) => {
            e.preventDefault();
            this.startDrawing(e.touches[0]);
        }, { passive: false });

        this.canvas.addEventListener('touchmove', (e) => {
            e.preventDefault();
            this.draw(e.touches[0]);
        }, { passive: false });

        this.canvas.addEventListener('touchend', () => this.stopDrawing());
        this.canvas.addEventListener('touchcancel', () => this.stopDrawing());
    }

    getCanvasCoordinates(e) {
        const rect = this.canvas.getBoundingClientRect();
        const scaleX = this.canvas.width / rect.width;
        const scaleY = this.canvas.height / rect.height;

        return {
            x: (e.clientX - rect.left) * scaleX,
            y: (e.clientY - rect.top) * scaleY
        };
    }

    startDrawing(e) {
        if (!this.isEnabled) return;

        this.isDrawing = true;
        const coords = this.getCanvasCoordinates(e);
        this.lastX = coords.x;
        this.lastY = coords.y;

        // Dibujar un punto inicial
        this.ctx.beginPath();
        this.ctx.arc(this.lastX, this.lastY, this.brushSize / 2, 0, Math.PI * 2);

        if (this.tool === 'eraser') {
            this.ctx.globalCompositeOperation = 'destination-out';
            this.ctx.fillStyle = 'rgba(0,0,0,1)';
        } else {
            this.ctx.globalCompositeOperation = 'source-over';
            this.ctx.fillStyle = this.hexToRgba(this.brushColor, this.opacity);
        }

        this.ctx.fill();
    }

    draw(e) {
        if (!this.isDrawing || !this.isEnabled) return;

        const coords = this.getCanvasCoordinates(e);

        this.ctx.beginPath();
        this.ctx.moveTo(this.lastX, this.lastY);
        this.ctx.lineTo(coords.x, coords.y);

        this.ctx.lineWidth = this.brushSize;
        this.ctx.lineCap = 'round';
        this.ctx.lineJoin = 'round';

        if (this.tool === 'eraser') {
            this.ctx.globalCompositeOperation = 'destination-out';
            this.ctx.strokeStyle = 'rgba(0,0,0,1)';
        } else {
            this.ctx.globalCompositeOperation = 'source-over';
            this.ctx.strokeStyle = this.hexToRgba(this.brushColor, this.opacity);
        }

        this.ctx.stroke();

        this.lastX = coords.x;
        this.lastY = coords.y;
    }

    stopDrawing() {
        if (this.isDrawing) {
            this.isDrawing = false;
            this.saveToHistory();
        }
    }

    hexToRgba(hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    saveToHistory() {
        // Eliminar estados futuros si estamos en medio del historial
        if (this.historyIndex < this.history.length - 1) {
            this.history = this.history.slice(0, this.historyIndex + 1);
        }

        // Guardar estado actual
        const imageData = this.canvas.toDataURL();
        this.history.push(imageData);

        // Limitar historial
        if (this.history.length > this.maxHistory) {
            this.history.shift();
        } else {
            this.historyIndex++;
        }

        this.updateUndoRedoButtons();
    }

    undo() {
        if (this.historyIndex > 0) {
            this.historyIndex--;
            this.loadFromHistory();
        } else if (this.historyIndex === 0) {
            // Volver al estado vacío
            this.historyIndex = -1;
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        }
        this.updateUndoRedoButtons();
    }

    redo() {
        if (this.historyIndex < this.history.length - 1) {
            this.historyIndex++;
            this.loadFromHistory();
        }
        this.updateUndoRedoButtons();
    }

    loadFromHistory() {
        const img = new Image();
        img.onload = () => {
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
            this.ctx.drawImage(img, 0, 0);
        };
        img.src = this.history[this.historyIndex];
    }

    updateUndoRedoButtons() {
        const undoBtn = this.panel.querySelector('#undoBtn');
        const redoBtn = this.panel.querySelector('#redoBtn');

        if (undoBtn) {
            undoBtn.disabled = this.historyIndex < 0;
            undoBtn.classList.toggle('disabled', this.historyIndex < 0);
        }
        if (redoBtn) {
            redoBtn.disabled = this.historyIndex >= this.history.length - 1;
            redoBtn.classList.toggle('disabled', this.historyIndex >= this.history.length - 1);
        }
    }

    clearCanvas() {
        if (confirm('¿Estás seguro de que quieres borrar todo el dibujo?')) {
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
            this.history = [];
            this.historyIndex = -1;
            this.updateUndoRedoButtons();
        }
    }

    toggleDrawingMode() {
        this.isEnabled = !this.isEnabled;

        const modeBtn = this.panel.querySelector('#toggleDrawingMode');
        const modeText = this.panel.querySelector('#drawingModeText');

        if (this.isEnabled) {
            this.canvas.style.pointerEvents = 'auto';
            modeBtn.classList.add('active');
            modeText.textContent = 'Desactivar Dibujo';
            this.updateCursor();
        } else {
            this.canvas.style.pointerEvents = 'none';
            modeBtn.classList.remove('active');
            modeText.textContent = 'Activar Dibujo';
            this.canvas.style.cursor = 'default';
        }
    }

    updateCursor() {
        if (!this.isEnabled) return;

        // Crear cursor personalizado basado en el tamaño del pincel
        const size = Math.max(this.brushSize, 10);
        const color = this.tool === 'eraser' ? '#ffffff' : this.brushColor;

        const cursorCanvas = document.createElement('canvas');
        cursorCanvas.width = size + 4;
        cursorCanvas.height = size + 4;
        const ctx = cursorCanvas.getContext('2d');

        // Dibujar círculo del cursor
        ctx.beginPath();
        ctx.arc(size/2 + 2, size/2 + 2, size/2, 0, Math.PI * 2);
        ctx.strokeStyle = this.tool === 'eraser' ? '#000' : '#fff';
        ctx.lineWidth = 2;
        ctx.stroke();

        ctx.beginPath();
        ctx.arc(size/2 + 2, size/2 + 2, size/2 - 1, 0, Math.PI * 2);
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.stroke();

        if (this.tool !== 'eraser') {
            ctx.beginPath();
            ctx.arc(size/2 + 2, size/2 + 2, 2, 0, Math.PI * 2);
            ctx.fillStyle = color;
            ctx.fill();
        }

        const cursorUrl = cursorCanvas.toDataURL();
        this.canvas.style.cursor = `url(${cursorUrl}) ${size/2 + 2} ${size/2 + 2}, crosshair`;
    }

    resizeCanvas(width, height) {
        // Guardar contenido actual
        const tempCanvas = document.createElement('canvas');
        tempCanvas.width = this.canvas.width;
        tempCanvas.height = this.canvas.height;
        tempCanvas.getContext('2d').drawImage(this.canvas, 0, 0);

        // Redimensionar
        this.canvas.width = width;
        this.canvas.height = height;

        // Restaurar contenido escalado
        if (tempCanvas.width > 0 && tempCanvas.height > 0) {
            this.ctx.drawImage(tempCanvas, 0, 0, width, height);
        }
    }

    show(container) {
        if (!this.panel.parentElement) {
            container.appendChild(this.panel);
        }
        this.panel.classList.add('visible');
        this.isVisible = true;
        this.updateUndoRedoButtons();
    }

    hide() {
        this.panel.classList.remove('visible');
        this.isVisible = false;
    }

    toggle(container) {
        if (this.isVisible) {
            this.hide();
        } else {
            this.show(container);
        }
    }

    mount(previewContainer) {
        // Buscar el elemento media (imagen o video)
        const mediaElement = previewContainer.querySelector('#editorMedia');

        if (!this.canvas.parentElement) {
            previewContainer.appendChild(this.canvas);
            this.bindCanvasEvents();
        }

        // Función para ajustar el canvas al media
        const adjustCanvasToMedia = () => {
            if (mediaElement) {
                const mediaRect = mediaElement.getBoundingClientRect();
                const containerRect = previewContainer.getBoundingClientRect();

                // Posicionar canvas sobre el media
                this.canvas.style.position = 'absolute';
                this.canvas.style.left = (mediaRect.left - containerRect.left) + 'px';
                this.canvas.style.top = (mediaRect.top - containerRect.top) + 'px';
                this.canvas.style.width = mediaRect.width + 'px';
                this.canvas.style.height = mediaRect.height + 'px';

                // Ajustar resolución del canvas para buena calidad de dibujo
                const dpr = window.devicePixelRatio || 1;
                this.canvas.width = mediaRect.width * dpr;
                this.canvas.height = mediaRect.height * dpr;
                this.ctx.scale(dpr, dpr);
            } else {
                // Fallback: ajustar al contenedor
                const rect = previewContainer.getBoundingClientRect();
                this.canvas.style.position = 'absolute';
                this.canvas.style.left = '0';
                this.canvas.style.top = '0';
                this.canvas.style.width = rect.width + 'px';
                this.canvas.style.height = rect.height + 'px';
                this.canvas.width = rect.width;
                this.canvas.height = rect.height;
            }
        };

        // Ajustar inmediatamente
        adjustCanvasToMedia();

        // Re-ajustar si el media es una imagen que aún no ha cargado
        if (mediaElement && mediaElement.tagName === 'IMG') {
            if (!mediaElement.complete) {
                mediaElement.onload = adjustCanvasToMedia;
            }
        }

        // Guardar referencia para re-ajustar en resize
        this._resizeHandler = adjustCanvasToMedia;
        window.addEventListener('resize', this._resizeHandler);
    }

    unmount() {
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
            this._resizeHandler = null;
        }
        if (this.canvas.parentElement) {
            this.canvas.remove();
        }
    }

    // Obtener el canvas para exportación
    getCanvas() {
        return this.canvas;
    }

    // Verificar si hay dibujo
    hasDrawing() {
        const imageData = this.ctx.getImageData(0, 0, this.canvas.width, this.canvas.height);
        return imageData.data.some((value, index) => index % 4 === 3 && value > 0);
    }

    // Renderizar dibujo en un canvas destino
    renderToCanvas(targetCanvas) {
        const ctx = targetCanvas.getContext('2d');
        ctx.drawImage(this.canvas, 0, 0, targetCanvas.width, targetCanvas.height);
    }

    // Limpiar todo
    reset() {
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        this.history = [];
        this.historyIndex = -1;
        this.isEnabled = false;
        this.canvas.style.pointerEvents = 'none';

        const modeBtn = this.panel.querySelector('#toggleDrawingMode');
        const modeText = this.panel.querySelector('#drawingModeText');
        if (modeBtn) modeBtn.classList.remove('active');
        if (modeText) modeText.textContent = 'Activar Dibujo';

        this.updateUndoRedoButtons();
    }

    destroy() {
        // Remover keyboard listener para evitar memory leak
        if (this.keyboardHandler) {
            document.removeEventListener('keydown', this.keyboardHandler);
            this.keyboardHandler = null;
        }
        // Remover resize listener
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
            this._resizeHandler = null;
        }
        this.canvas.remove();
        this.panel.remove();
    }
}
