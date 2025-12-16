/**
 * MemeMode.js - Modo Meme para ReelsCreator
 * Permite crear memes con texto estilo Impact, efectos y props
 */

class MemeMode {
    constructor(layerManager, filterEngine) {
        this.layerManager = layerManager;
        this.filterEngine = filterEngine;
        this.panel = null;
        this.isVisible = false;

        // Estado del meme
        this.memeTexts = [];
        this.textPosition = 'top-bottom'; // 'top', 'bottom', 'center', 'top-bottom', 'free'
        this.fontSize = 48;
        this.strokeWidth = 3;
        this.uppercase = true;
        this.currentEffect = null;
        this.effectIntensity = 50;
        this.selectedTemplate = null;
        this.showWatermark = false;
        this.activeTextLayer = null;

        // Efectos meme disponibles
        this.memeEffects = {
            'deepfry': {
                name: 'Deep Fry',
                icon: '🔥',
                apply: (intensity) => `saturate(${200 + intensity * 3}%) contrast(${100 + intensity}%) brightness(${100 + intensity * 0.3}%)`
            },
            'jpeg': {
                name: 'JPEG',
                icon: '📸',
                apply: (intensity) => `blur(${intensity * 0.02}px) contrast(${100 + intensity * 0.5}%)`
            },
            'pixel': {
                name: 'Pixel',
                icon: '🎮',
                apply: (intensity) => this.createPixelEffect(intensity)
            },
            'vignette': {
                name: 'Vignette',
                icon: '🌑',
                apply: (intensity) => `brightness(${100 - intensity * 0.2}%)`
            },
            'vintage': {
                name: 'Vintage',
                icon: '📷',
                apply: (intensity) => `sepia(${intensity}%) contrast(${90 + intensity * 0.1}%)`
            },
            'neon': {
                name: 'Neon',
                icon: '💡',
                apply: (intensity) => `saturate(${150 + intensity * 2}%) brightness(${100 + intensity * 0.5}%) hue-rotate(${intensity}deg)`
            }
        };

        // Props meme disponibles
        this.memeProps = [
            { id: 'deal-with-it', name: 'Deal With It', emoji: '🕶️' },
            { id: 'thug-life', name: 'Thug Life', emoji: '😎' },
            { id: 'crying', name: 'Llorando', emoji: '😭' },
            { id: 'thinking', name: 'Pensando', emoji: '🤔' },
            { id: '100', name: '100', emoji: '💯' },
            { id: 'fire', name: 'Fuego', emoji: '🔥' },
            { id: 'skull', name: 'Skull', emoji: '💀' },
            { id: 'clown', name: 'Payaso', emoji: '🤡' },
            { id: 'cap', name: 'Cap', emoji: '🧢' },
            { id: 'crown', name: 'Corona', emoji: '👑' },
            { id: 'star', name: 'Estrella', emoji: '⭐' },
            { id: 'sparkles', name: 'Brillos', emoji: '✨' }
        ];

        this.init();
    }

    init() {
        this.createPanel();
    }

    createPanel() {
        this.panel = document.createElement('div');
        this.panel.className = 'meme-mode-panel editor-panel';
        this.panel.innerHTML = `
            <div class="panel-header meme-panel-header">
                <h3>🎭 Modo Meme</h3>
                <button class="panel-close-btn" id="memePanelClose">&times;</button>
            </div>

            <div class="panel-content">
                <!-- Sección de Texto Meme -->
                <div class="panel-section">
                    <h4 class="section-title">Texto Meme</h4>

                    <div class="meme-text-input-group">
                        <input type="text" id="memeTextTop" class="meme-text-input" placeholder="TEXTO SUPERIOR">
                        <input type="text" id="memeTextBottom" class="meme-text-input" placeholder="TEXTO INFERIOR">
                    </div>

                    <div class="meme-position-buttons">
                        <button class="meme-pos-btn active" data-position="top-bottom" title="Arriba y Abajo">
                            <span>⬆️⬇️</span>
                        </button>
                        <button class="meme-pos-btn" data-position="top" title="Solo Arriba">
                            <span>⬆️</span>
                        </button>
                        <button class="meme-pos-btn" data-position="bottom" title="Solo Abajo">
                            <span>⬇️</span>
                        </button>
                        <button class="meme-pos-btn" data-position="center" title="Centro">
                            <span>🎯</span>
                        </button>
                    </div>

                    <div class="meme-text-controls">
                        <div class="control-row">
                            <label>Tamaño: <span id="fontSizeValue">${this.fontSize}px</span></label>
                            <input type="range" id="memeFontSize" min="20" max="100" value="${this.fontSize}">
                        </div>
                        <div class="control-row">
                            <label>Borde: <span id="strokeValue">${this.strokeWidth}px</span></label>
                            <input type="range" id="memeStrokeWidth" min="1" max="8" value="${this.strokeWidth}">
                        </div>
                        <div class="control-row checkbox-row">
                            <label>
                                <input type="checkbox" id="memeUppercase" ${this.uppercase ? 'checked' : ''}>
                                MAYÚSCULAS
                            </label>
                        </div>
                    </div>

                    <button class="meme-apply-text-btn" id="applyMemeText">
                        Aplicar Texto
                    </button>
                </div>

                <!-- Sección de Efectos -->
                <div class="panel-section">
                    <h4 class="section-title">Efectos Meme</h4>
                    <div class="meme-effects-grid" id="memeEffectsGrid">
                        ${this.renderEffectButtons()}
                    </div>
                    <div class="effect-intensity-control" id="effectIntensityControl" style="display: none;">
                        <label>Intensidad: <span id="intensityValue">50%</span></label>
                        <input type="range" id="effectIntensity" min="0" max="100" value="50">
                        <button class="remove-effect-btn" id="removeEffect">Quitar Efecto</button>
                    </div>
                </div>

                <!-- Sección de Props -->
                <div class="panel-section">
                    <h4 class="section-title">Props Meme</h4>
                    <div class="meme-props-grid" id="memePropsGrid">
                        ${this.renderPropButtons()}
                    </div>
                </div>

                <!-- Sección de Watermark -->
                <div class="panel-section watermark-section">
                    <label class="watermark-toggle">
                        <input type="checkbox" id="memeWatermark" ${this.showWatermark ? 'checked' : ''}>
                        <span>Marca de agua "Lado"</span>
                    </label>
                </div>
            </div>
        `;

        // Bind events
        this.bindPanelEvents();
    }

    renderEffectButtons() {
        return Object.entries(this.memeEffects).map(([id, effect]) => `
            <button class="meme-effect-btn" data-effect="${id}" title="${effect.name}">
                <span class="effect-icon">${effect.icon}</span>
                <span class="effect-name">${effect.name}</span>
            </button>
        `).join('');
    }

    renderPropButtons() {
        return this.memeProps.map(prop => `
            <button class="meme-prop-btn" data-prop="${prop.id}" title="${prop.name}">
                <span class="prop-emoji">${prop.emoji}</span>
            </button>
        `).join('');
    }

    bindPanelEvents() {
        // Close button
        this.panel.querySelector('#memePanelClose').addEventListener('click', () => this.hide());

        // Text inputs - actualizar en tiempo real
        const topInput = this.panel.querySelector('#memeTextTop');
        const bottomInput = this.panel.querySelector('#memeTextBottom');

        topInput.addEventListener('input', () => this.updateMemeTextPreview());
        bottomInput.addEventListener('input', () => this.updateMemeTextPreview());

        // Position buttons
        this.panel.querySelectorAll('.meme-pos-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.meme-pos-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.textPosition = btn.dataset.position;
                this.updateMemeTextPreview();
            });
        });

        // Font size slider
        const fontSizeSlider = this.panel.querySelector('#memeFontSize');
        fontSizeSlider.addEventListener('input', (e) => {
            this.fontSize = parseInt(e.target.value);
            this.panel.querySelector('#fontSizeValue').textContent = `${this.fontSize}px`;
            this.updateMemeTextPreview();
        });

        // Stroke width slider
        const strokeSlider = this.panel.querySelector('#memeStrokeWidth');
        strokeSlider.addEventListener('input', (e) => {
            this.strokeWidth = parseInt(e.target.value);
            this.panel.querySelector('#strokeValue').textContent = `${this.strokeWidth}px`;
            this.updateMemeTextPreview();
        });

        // Uppercase checkbox
        const uppercaseCheck = this.panel.querySelector('#memeUppercase');
        uppercaseCheck.addEventListener('change', (e) => {
            this.uppercase = e.target.checked;
            this.updateMemeTextPreview();
        });

        // Apply text button
        this.panel.querySelector('#applyMemeText').addEventListener('click', () => this.applyMemeText());

        // Effect buttons
        this.panel.querySelectorAll('.meme-effect-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const effectId = btn.dataset.effect;
                this.selectEffect(effectId, btn);
            });
        });

        // Effect intensity slider
        const intensitySlider = this.panel.querySelector('#effectIntensity');
        intensitySlider.addEventListener('input', (e) => {
            this.effectIntensity = parseInt(e.target.value);
            this.panel.querySelector('#intensityValue').textContent = `${this.effectIntensity}%`;
            this.applyCurrentEffect();
        });

        // Remove effect button
        this.panel.querySelector('#removeEffect').addEventListener('click', () => this.removeEffect());

        // Prop buttons
        this.panel.querySelectorAll('.meme-prop-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const propId = btn.dataset.prop;
                this.addMemeProp(propId);
            });
        });

        // Watermark checkbox
        const watermarkCheck = this.panel.querySelector('#memeWatermark');
        watermarkCheck.addEventListener('change', (e) => {
            this.showWatermark = e.target.checked;
            this.toggleWatermark();
        });
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
    }

    toggle(container) {
        if (this.isVisible) {
            this.hide();
        } else {
            this.show(container);
        }
    }

    updateMemeTextPreview() {
        // Remover textos meme anteriores
        this.clearMemeTexts();

        const topText = this.panel.querySelector('#memeTextTop').value;
        const bottomText = this.panel.querySelector('#memeTextBottom').value;

        if (topText || bottomText) {
            if (this.textPosition === 'top-bottom') {
                if (topText) this.createMemeTextLayer(topText, 'top');
                if (bottomText) this.createMemeTextLayer(bottomText, 'bottom');
            } else if (this.textPosition === 'top' && topText) {
                this.createMemeTextLayer(topText, 'top');
            } else if (this.textPosition === 'bottom' && (bottomText || topText)) {
                this.createMemeTextLayer(bottomText || topText, 'bottom');
            } else if (this.textPosition === 'center' && (topText || bottomText)) {
                this.createMemeTextLayer(topText || bottomText, 'center');
            }
        }
    }

    createMemeTextLayer(text, position) {
        if (!this.layerManager) return;

        const displayText = this.uppercase ? text.toUpperCase() : text;
        const container = this.layerManager.container;
        const containerRect = container.getBoundingClientRect();

        // Crear elemento de texto meme
        const textEl = document.createElement('div');
        textEl.className = 'meme-text-layer';
        textEl.dataset.memeText = 'true';
        textEl.dataset.position = position;

        // Aplicar estilos meme
        textEl.style.cssText = `
            font-family: 'Impact', 'Anton', 'Arial Black', sans-serif;
            font-size: ${this.fontSize}px;
            color: white;
            text-align: center;
            text-transform: ${this.uppercase ? 'uppercase' : 'none'};
            -webkit-text-stroke: ${this.strokeWidth}px black;
            text-shadow:
                ${this.strokeWidth}px ${this.strokeWidth}px 0 #000,
                -${this.strokeWidth}px ${this.strokeWidth}px 0 #000,
                -${this.strokeWidth}px -${this.strokeWidth}px 0 #000,
                ${this.strokeWidth}px -${this.strokeWidth}px 0 #000,
                0 ${this.strokeWidth}px 0 #000,
                0 -${this.strokeWidth}px 0 #000,
                ${this.strokeWidth}px 0 0 #000,
                -${this.strokeWidth}px 0 0 #000;
            letter-spacing: 2px;
            line-height: 1.1;
            position: absolute;
            left: 50%;
            transform: translateX(-50%);
            width: 90%;
            word-wrap: break-word;
            pointer-events: auto;
            cursor: move;
            user-select: none;
            z-index: 100;
        `;

        // Posición vertical
        switch (position) {
            case 'top':
                textEl.style.top = '5%';
                break;
            case 'bottom':
                textEl.style.bottom = '5%';
                textEl.style.top = 'auto';
                break;
            case 'center':
                textEl.style.top = '50%';
                textEl.style.transform = 'translate(-50%, -50%)';
                break;
        }

        textEl.textContent = displayText;

        // Agregar al contenedor
        container.appendChild(textEl);

        // Hacer arrastrable
        this.makeTextDraggable(textEl, container);

        // Registrar en tracking
        this.memeTexts.push(textEl);

        return textEl;
    }

    makeTextDraggable(element, container) {
        let isDragging = false;
        let startX, startY, startLeft, startTop;

        element.addEventListener('mousedown', startDrag);
        element.addEventListener('touchstart', startDrag, { passive: false });

        function startDrag(e) {
            isDragging = true;
            element.style.cursor = 'grabbing';

            const touch = e.touches ? e.touches[0] : e;
            startX = touch.clientX;
            startY = touch.clientY;

            const rect = element.getBoundingClientRect();
            const containerRect = container.getBoundingClientRect();

            startLeft = rect.left - containerRect.left;
            startTop = rect.top - containerRect.top;

            // Cambiar a posicionamiento absoluto desde left/top
            element.style.left = startLeft + 'px';
            element.style.top = startTop + 'px';
            element.style.bottom = 'auto';
            element.style.transform = 'none';

            document.addEventListener('mousemove', drag);
            document.addEventListener('touchmove', drag, { passive: false });
            document.addEventListener('mouseup', stopDrag);
            document.addEventListener('touchend', stopDrag);

            e.preventDefault();
        }

        function drag(e) {
            if (!isDragging) return;

            const touch = e.touches ? e.touches[0] : e;
            const deltaX = touch.clientX - startX;
            const deltaY = touch.clientY - startY;

            element.style.left = (startLeft + deltaX) + 'px';
            element.style.top = (startTop + deltaY) + 'px';

            e.preventDefault();
        }

        function stopDrag() {
            isDragging = false;
            element.style.cursor = 'move';
            document.removeEventListener('mousemove', drag);
            document.removeEventListener('touchmove', drag);
            document.removeEventListener('mouseup', stopDrag);
            document.removeEventListener('touchend', stopDrag);
        }
    }

    clearMemeTexts() {
        this.memeTexts.forEach(el => {
            if (el.parentElement) {
                el.parentElement.removeChild(el);
            }
        });
        this.memeTexts = [];
    }

    applyMemeText() {
        // Los textos ya están aplicados en tiempo real
        // Este botón sirve para confirmar y agregar como capa permanente

        if (this.memeTexts.length > 0) {
            // Marcar como aplicados (ya no se removerán al cambiar inputs)
            this.memeTexts.forEach(el => {
                el.dataset.applied = 'true';
            });

            // Limpiar inputs
            this.panel.querySelector('#memeTextTop').value = '';
            this.panel.querySelector('#memeTextBottom').value = '';

            // Resetear tracking para nuevos textos
            this.memeTexts = [];

            this.showNotification('Texto meme aplicado');
        }
    }

    selectEffect(effectId, buttonElement) {
        // Deseleccionar otros
        this.panel.querySelectorAll('.meme-effect-btn').forEach(b => b.classList.remove('active'));
        buttonElement.classList.add('active');

        this.currentEffect = effectId;

        // Mostrar control de intensidad
        this.panel.querySelector('#effectIntensityControl').style.display = 'block';

        this.applyCurrentEffect();
    }

    applyCurrentEffect() {
        if (!this.currentEffect) return;

        const effect = this.memeEffects[this.currentEffect];
        if (!effect) return;

        const mediaElement = document.getElementById('editorMedia');
        if (!mediaElement) return;

        const filterValue = effect.apply(this.effectIntensity);

        // Para el efecto pixel, aplicamos de forma especial
        if (this.currentEffect === 'pixel') {
            this.applyPixelEffect(mediaElement, this.effectIntensity);
        } else {
            mediaElement.style.filter = filterValue;
        }
    }

    createPixelEffect(intensity) {
        // El pixelado se hace via CSS en navegadores modernos con image-rendering
        return `contrast(${100 + intensity * 0.3}%)`;
    }

    applyPixelEffect(element, intensity) {
        // Usar CSS pixelation
        const pixelSize = Math.max(1, Math.floor(intensity / 10));
        element.style.imageRendering = intensity > 30 ? 'pixelated' : 'auto';
        element.style.filter = `contrast(${100 + intensity * 0.3}%)`;

        // Si el pixelado es intenso, añadir class especial
        if (intensity > 50) {
            element.classList.add('meme-pixelated');
        } else {
            element.classList.remove('meme-pixelated');
        }
    }

    removeEffect() {
        this.currentEffect = null;
        this.panel.querySelectorAll('.meme-effect-btn').forEach(b => b.classList.remove('active'));
        this.panel.querySelector('#effectIntensityControl').style.display = 'none';

        const mediaElement = document.getElementById('editorMedia');
        if (mediaElement) {
            mediaElement.style.filter = '';
            mediaElement.style.imageRendering = '';
            mediaElement.classList.remove('meme-pixelated');
        }
    }

    addMemeProp(propId) {
        const prop = this.memeProps.find(p => p.id === propId);
        if (!prop || !this.layerManager) return;

        const container = this.layerManager.container;

        // Crear elemento de prop (usando emoji grande)
        const propEl = document.createElement('div');
        propEl.className = 'meme-prop-layer';
        propEl.dataset.propId = propId;
        propEl.innerHTML = `<span class="prop-content">${prop.emoji}</span>`;
        propEl.style.cssText = `
            position: absolute;
            font-size: 64px;
            cursor: move;
            user-select: none;
            z-index: 101;
            left: 50%;
            top: 50%;
            transform: translate(-50%, -50%);
            filter: drop-shadow(2px 2px 4px rgba(0,0,0,0.5));
        `;

        container.appendChild(propEl);

        // Hacer arrastrable y redimensionable
        this.makeTextDraggable(propEl, container);
        this.makeResizable(propEl);
    }

    makeResizable(element) {
        let isResizing = false;
        let startSize, startX;

        // Doble tap/click para redimensionar
        let lastTap = 0;
        element.addEventListener('touchend', (e) => {
            const currentTime = new Date().getTime();
            const tapLength = currentTime - lastTap;
            if (tapLength < 300 && tapLength > 0) {
                this.showResizeControls(element);
            }
            lastTap = currentTime;
        });

        element.addEventListener('dblclick', () => {
            this.showResizeControls(element);
        });

        // Permitir zoom con scroll del mouse
        element.addEventListener('wheel', (e) => {
            e.preventDefault();
            const currentSize = parseInt(window.getComputedStyle(element).fontSize);
            const delta = e.deltaY > 0 ? -5 : 5;
            const newSize = Math.max(20, Math.min(200, currentSize + delta));
            element.style.fontSize = newSize + 'px';
        });
    }

    showResizeControls(element) {
        // Crear slider de tamaño temporal
        const existingControls = document.querySelector('.prop-resize-controls');
        if (existingControls) existingControls.remove();

        const controls = document.createElement('div');
        controls.className = 'prop-resize-controls';
        controls.innerHTML = `
            <input type="range" min="20" max="200" value="${parseInt(window.getComputedStyle(element).fontSize)}">
            <button class="delete-prop-btn">🗑️</button>
        `;

        controls.querySelector('input').addEventListener('input', (e) => {
            element.style.fontSize = e.target.value + 'px';
        });

        controls.querySelector('.delete-prop-btn').addEventListener('click', () => {
            element.remove();
            controls.remove();
        });

        // Posicionar cerca del elemento
        const rect = element.getBoundingClientRect();
        controls.style.cssText = `
            position: fixed;
            left: ${rect.left}px;
            top: ${rect.bottom + 10}px;
            z-index: 1000;
            background: rgba(0,0,0,0.8);
            padding: 8px;
            border-radius: 8px;
            display: flex;
            gap: 8px;
            align-items: center;
        `;

        document.body.appendChild(controls);

        // Cerrar al hacer clic fuera
        setTimeout(() => {
            document.addEventListener('click', function closeControls(e) {
                if (!controls.contains(e.target) && e.target !== element) {
                    controls.remove();
                    document.removeEventListener('click', closeControls);
                }
            });
        }, 100);
    }

    toggleWatermark() {
        const container = this.layerManager?.container;
        if (!container) return;

        // Remover watermark existente
        const existingWatermark = container.querySelector('.lado-watermark');
        if (existingWatermark) {
            existingWatermark.remove();
        }

        if (this.showWatermark) {
            const watermark = document.createElement('div');
            watermark.className = 'lado-watermark';
            watermark.innerHTML = `
                <span class="watermark-text">Hecho con Lado</span>
            `;
            watermark.style.cssText = `
                position: absolute;
                bottom: 10px;
                right: 10px;
                font-family: 'Segoe UI', sans-serif;
                font-size: 12px;
                color: rgba(255,255,255,0.7);
                background: rgba(0,0,0,0.3);
                padding: 4px 8px;
                border-radius: 4px;
                z-index: 150;
                pointer-events: none;
            `;
            container.appendChild(watermark);
        }
    }

    showNotification(message) {
        const notification = document.createElement('div');
        notification.className = 'meme-notification';
        notification.textContent = message;
        notification.style.cssText = `
            position: fixed;
            bottom: 100px;
            left: 50%;
            transform: translateX(-50%);
            background: rgba(0,0,0,0.8);
            color: white;
            padding: 12px 24px;
            border-radius: 20px;
            font-size: 14px;
            z-index: 10000;
            animation: fadeInOut 2s ease-in-out forwards;
        `;

        document.body.appendChild(notification);
        setTimeout(() => notification.remove(), 2000);
    }

    /**
     * Renderiza todos los elementos meme a un canvas para exportación
     */
    renderToCanvas(canvas) {
        const ctx = canvas.getContext('2d');
        const container = this.layerManager?.container;
        if (!container) return;

        const containerRect = container.getBoundingClientRect();
        const scaleX = canvas.width / containerRect.width;
        const scaleY = canvas.height / containerRect.height;

        // Renderizar textos meme aplicados
        const memeTexts = container.querySelectorAll('.meme-text-layer[data-applied="true"]');
        memeTexts.forEach(textEl => {
            const rect = textEl.getBoundingClientRect();
            const x = (rect.left - containerRect.left + rect.width / 2) * scaleX;
            const y = (rect.top - containerRect.top + rect.height / 2) * scaleY;

            const computedStyle = window.getComputedStyle(textEl);
            const fontSize = parseFloat(computedStyle.fontSize) * scaleX;

            ctx.save();
            ctx.font = `bold ${fontSize}px Impact, 'Arial Black', sans-serif`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';

            // Stroke (borde negro)
            ctx.strokeStyle = 'black';
            ctx.lineWidth = this.strokeWidth * 2 * scaleX;
            ctx.lineJoin = 'round';
            ctx.strokeText(textEl.textContent, x, y);

            // Fill (relleno blanco)
            ctx.fillStyle = 'white';
            ctx.fillText(textEl.textContent, x, y);

            ctx.restore();
        });

        // Renderizar props
        const props = container.querySelectorAll('.meme-prop-layer');
        props.forEach(propEl => {
            const rect = propEl.getBoundingClientRect();
            const x = (rect.left - containerRect.left + rect.width / 2) * scaleX;
            const y = (rect.top - containerRect.top + rect.height / 2) * scaleY;

            const fontSize = parseFloat(window.getComputedStyle(propEl).fontSize) * scaleX;

            ctx.save();
            ctx.font = `${fontSize}px sans-serif`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(propEl.textContent.trim(), x, y);
            ctx.restore();
        });

        // Renderizar watermark
        if (this.showWatermark) {
            ctx.save();
            ctx.font = `${12 * scaleX}px 'Segoe UI', sans-serif`;
            ctx.fillStyle = 'rgba(255,255,255,0.7)';
            ctx.textAlign = 'right';
            ctx.textBaseline = 'bottom';
            ctx.fillText('Hecho con Lado', canvas.width - 10 * scaleX, canvas.height - 10 * scaleY);
            ctx.restore();
        }
    }

    reset() {
        this.clearMemeTexts();
        this.removeEffect();
        this.showWatermark = false;

        if (this.panel) {
            this.panel.querySelector('#memeTextTop').value = '';
            this.panel.querySelector('#memeTextBottom').value = '';
            this.panel.querySelector('#memeWatermark').checked = false;
        }

        // Remover props y watermark del contenedor
        const container = this.layerManager?.container;
        if (container) {
            container.querySelectorAll('.meme-prop-layer, .lado-watermark').forEach(el => el.remove());
        }
    }

    destroy() {
        this.reset();
        if (this.panel && this.panel.parentElement) {
            this.panel.parentElement.removeChild(this.panel);
        }
    }
}

// Export global
window.MemeMode = MemeMode;
