/**
 * TextLayer.js - Herramienta de Texto
 * Panel de edición de texto con fuentes, colores y estilos
 */

class TextLayer {
    constructor(layerManager) {
        this.layerManager = layerManager;
        this.panel = null;
        this.isVisible = false;

        // Fuentes disponibles
        this.fonts = [
            { name: 'Inter', display: 'Inter' },
            { name: 'Arial', display: 'Arial' },
            { name: 'Georgia', display: 'Georgia' },
            { name: 'Times New Roman', display: 'Times' },
            { name: 'Courier New', display: 'Courier' },
            { name: 'Verdana', display: 'Verdana' },
            { name: 'Impact', display: 'Impact' },
            { name: 'Comic Sans MS', display: 'Comic' }
        ];

        // Colores predefinidos
        this.colors = [
            '#ffffff', '#000000', '#ff0000', '#ff6b6b',
            '#ffa500', '#ffeb3b', '#4caf50', '#00bcd4',
            '#2196f3', '#3f51b5', '#9c27b0', '#e91e63',
            '#795548', '#607d8b', '#f5f5f5', '#333333'
        ];

        // Fondos predefinidos
        this.backgrounds = [
            'transparent', '#000000', '#ffffff', '#ff0000',
            '#ffa500', '#ffeb3b', '#4caf50', '#2196f3',
            '#9c27b0', 'rgba(0,0,0,0.5)', 'rgba(255,255,255,0.5)'
        ];

        this.init();
    }

    init() {
        this.createPanel();
    }

    createPanel() {
        this.panel = document.createElement('div');
        this.panel.className = 'text-editor-panel';
        this.panel.innerHTML = `
            <div class="text-panel-header">
                <span class="text-panel-title">Texto</span>
                <button class="text-panel-close" id="closeTextPanel">&times;</button>
            </div>

            <div class="text-panel-content">
                <!-- Add Text Button -->
                <button class="add-text-btn" id="addTextBtn">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="12" y1="5" x2="12" y2="19"></line>
                        <line x1="5" y1="12" x2="19" y2="12"></line>
                    </svg>
                    Agregar Texto
                </button>

                <!-- Font Selection -->
                <div class="text-option-group">
                    <label>Fuente</label>
                    <div class="font-selector" id="fontSelector">
                        ${this.fonts.map(font => `
                            <button class="font-btn ${font.name === 'Inter' ? 'active' : ''}"
                                    data-font="${font.name}"
                                    style="font-family: ${font.name}">
                                ${font.display}
                            </button>
                        `).join('')}
                    </div>
                </div>

                <!-- Font Size -->
                <div class="text-option-group">
                    <label>Tamaño: <span id="fontSizeValue">32</span>px</label>
                    <input type="range" id="fontSizeSlider" min="16" max="72" value="32" class="text-slider">
                </div>

                <!-- Font Weight -->
                <div class="text-option-group">
                    <label>Estilo</label>
                    <div class="style-buttons">
                        <button class="style-btn active" data-weight="bold" title="Negrita">
                            <strong>B</strong>
                        </button>
                        <button class="style-btn" data-weight="normal" title="Normal">
                            N
                        </button>
                    </div>
                </div>

                <!-- Text Color -->
                <div class="text-option-group">
                    <label>Color de Texto</label>
                    <div class="color-palette" id="textColorPalette">
                        ${this.colors.map(color => `
                            <button class="color-btn ${color === '#ffffff' ? 'active' : ''}"
                                    data-color="${color}"
                                    style="background-color: ${color}">
                            </button>
                        `).join('')}
                    </div>
                </div>

                <!-- Background Color -->
                <div class="text-option-group">
                    <label>Fondo</label>
                    <div class="color-palette" id="bgColorPalette">
                        ${this.backgrounds.map((bg, i) => `
                            <button class="color-btn ${i === 0 ? 'active' : ''}"
                                    data-color="${bg}"
                                    style="background: ${bg === 'transparent' ? 'url(data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAGElEQVQYlWNgYGCQwoKxgqGgcJA5h3yFAAs8BRWVSwooAAAAAElFTkSuQmCC)' : bg}">
                            </button>
                        `).join('')}
                    </div>
                </div>

                <!-- Text Alignment -->
                <div class="text-option-group">
                    <label>Alineación</label>
                    <div class="align-buttons">
                        <button class="align-btn" data-align="left" title="Izquierda">
                            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 3h18v2H3V3zm0 8h12v2H3v-2zm0 8h18v2H3v-2zm0-4h12v2H3v-2z"/></svg>
                        </button>
                        <button class="align-btn active" data-align="center" title="Centro">
                            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 3h18v2H3V3zm3 8h12v2H6v-2zm-3 8h18v2H3v-2zm3-4h12v2H6v-2z"/></svg>
                        </button>
                        <button class="align-btn" data-align="right" title="Derecha">
                            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 3h18v2H3V3zm6 8h12v2H9v-2zm-6 8h18v2H3v-2zm6-4h12v2H9v-2z"/></svg>
                        </button>
                    </div>
                </div>

                <!-- Shadow Toggle -->
                <div class="text-option-group">
                    <label class="toggle-label">
                        <span>Sombra</span>
                        <label class="toggle-switch small">
                            <input type="checkbox" id="textShadowToggle" checked>
                            <span class="toggle-slider"></span>
                        </label>
                    </label>
                </div>

                <!-- Delete Button -->
                <button class="delete-text-btn" id="deleteTextBtn">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3 6 5 6 21 6"></polyline>
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                    </svg>
                    Eliminar Texto
                </button>
            </div>
        `;

        this.bindPanelEvents();
    }

    bindPanelEvents() {
        // Close panel
        this.panel.querySelector('#closeTextPanel').addEventListener('click', () => this.hide());

        // Add text
        this.panel.querySelector('#addTextBtn').addEventListener('click', () => this.addText());

        // Font selection
        this.panel.querySelectorAll('.font-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.font-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.updateSelectedText({ fontFamily: btn.dataset.font });
            });
        });

        // Font size
        const sizeSlider = this.panel.querySelector('#fontSizeSlider');
        const sizeValue = this.panel.querySelector('#fontSizeValue');
        sizeSlider.addEventListener('input', () => {
            sizeValue.textContent = sizeSlider.value;
            this.updateSelectedText({ fontSize: parseInt(sizeSlider.value) });
        });

        // Font weight
        this.panel.querySelectorAll('.style-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.style-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.updateSelectedText({ fontWeight: btn.dataset.weight });
            });
        });

        // Text color
        this.panel.querySelectorAll('#textColorPalette .color-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('#textColorPalette .color-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.updateSelectedText({ color: btn.dataset.color });
            });
        });

        // Background color
        this.panel.querySelectorAll('#bgColorPalette .color-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('#bgColorPalette .color-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.updateSelectedText({ backgroundColor: btn.dataset.color });
            });
        });

        // Alignment
        this.panel.querySelectorAll('.align-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.align-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.updateSelectedText({ textAlign: btn.dataset.align });
            });
        });

        // Shadow toggle
        this.panel.querySelector('#textShadowToggle').addEventListener('change', (e) => {
            this.updateSelectedText({ shadow: e.target.checked });
        });

        // Delete
        this.panel.querySelector('#deleteTextBtn').addEventListener('click', () => {
            this.layerManager.deleteSelectedLayer();
        });
    }

    addText() {
        this.layerManager.addTextLayer({
            text: 'Escribe aquí',
            fontSize: parseInt(this.panel.querySelector('#fontSizeSlider').value),
            fontFamily: this.panel.querySelector('.font-btn.active')?.dataset.font || 'Inter',
            fontWeight: this.panel.querySelector('.style-btn.active')?.dataset.weight || 'bold',
            color: this.panel.querySelector('#textColorPalette .color-btn.active')?.dataset.color || '#ffffff',
            backgroundColor: this.panel.querySelector('#bgColorPalette .color-btn.active')?.dataset.color || 'transparent',
            shadow: this.panel.querySelector('#textShadowToggle').checked
        });
    }

    updateSelectedText(properties) {
        this.layerManager.updateTextProperties(properties);
    }

    show(container) {
        if (!this.panel.parentElement) {
            container.appendChild(this.panel);
        }
        this.panel.classList.add('visible');
        this.isVisible = true;
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

    destroy() {
        this.panel.remove();
    }
}
