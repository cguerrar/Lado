/**
 * LayerManager.js - Sistema de Capas
 * Gestiona capas de texto, stickers y dibujo sobre el contenido
 */

class LayerManager {
    constructor(container) {
        this.container = container;
        this.canvas = null;
        this.ctx = null;
        this.layers = [];
        this.selectedLayer = null;
        this.isDragging = false;
        this.isResizing = false;
        this.dragOffset = { x: 0, y: 0 };
        this.resizeHandle = null;

        this.init();
    }

    init() {
        this.createCanvas();
        this.bindEvents();
    }

    /**
     * Crea el canvas overlay
     */
    createCanvas() {
        // Obtener dimensiones del contenedor
        const rect = this.container.getBoundingClientRect();

        this.canvas = document.createElement('canvas');
        this.canvas.id = 'layerCanvas';
        this.canvas.className = 'layer-canvas';
        this.canvas.width = rect.width;
        this.canvas.height = rect.height;

        this.ctx = this.canvas.getContext('2d');

        // Insertar canvas sobre el media
        this.container.style.position = 'relative';
        this.container.appendChild(this.canvas);

        // Crear contenedor de elementos interactivos
        this.elementsContainer = document.createElement('div');
        this.elementsContainer.className = 'layers-elements-container';
        this.container.appendChild(this.elementsContainer);
    }

    /**
     * Redimensiona el canvas al tamaño del contenedor
     */
    resize() {
        const rect = this.container.getBoundingClientRect();
        this.canvas.width = rect.width;
        this.canvas.height = rect.height;
        this.render();
    }

    /**
     * bindEvents
     */
    bindEvents() {
        // Click para deseleccionar
        this.canvas.addEventListener('click', (e) => {
            if (e.target === this.canvas) {
                this.deselectAll();
            }
        });

        // Resize observer
        if (window.ResizeObserver) {
            const observer = new ResizeObserver(() => this.resize());
            observer.observe(this.container);
        }
    }

    /**
     * Agrega una capa de texto
     */
    addTextLayer(options = {}) {
        const layer = {
            id: 'layer_' + Date.now(),
            type: 'text',
            text: options.text || 'Texto',
            x: options.x || this.canvas.width / 2,
            y: options.y || this.canvas.height / 2,
            fontSize: options.fontSize || 32,
            fontFamily: options.fontFamily || 'Inter',
            fontWeight: options.fontWeight || 'bold',
            color: options.color || '#ffffff',
            backgroundColor: options.backgroundColor || 'transparent',
            textAlign: options.textAlign || 'center',
            rotation: options.rotation || 0,
            scale: options.scale || 1,
            opacity: options.opacity || 1,
            shadow: options.shadow || true,
            element: null
        };

        this.layers.push(layer);
        this.createLayerElement(layer);
        this.selectLayer(layer);

        return layer;
    }

    /**
     * Agrega una capa de sticker
     */
    addStickerLayer(options = {}) {
        const layer = {
            id: 'layer_' + Date.now(),
            type: 'sticker',
            src: options.src || '',
            emoji: options.emoji || '',
            x: options.x || this.canvas.width / 2,
            y: options.y || this.canvas.height / 2,
            width: options.width || 80,
            height: options.height || 80,
            rotation: options.rotation || 0,
            scale: options.scale || 1,
            opacity: options.opacity || 1,
            element: null
        };

        this.layers.push(layer);
        this.createLayerElement(layer);
        this.selectLayer(layer);

        return layer;
    }

    /**
     * Crea el elemento DOM interactivo para una capa
     */
    createLayerElement(layer) {
        const element = document.createElement('div');
        element.className = 'layer-element';
        element.dataset.layerId = layer.id;

        if (layer.type === 'text') {
            element.innerHTML = `
                <div class="layer-content text-layer-content" contenteditable="false">${layer.text}</div>
                <div class="layer-handles">
                    <div class="handle handle-rotate" data-handle="rotate"></div>
                    <div class="handle handle-resize handle-br" data-handle="resize-br"></div>
                </div>
            `;
            this.applyTextStyles(element, layer);
        } else if (layer.type === 'sticker') {
            if (layer.emoji) {
                element.innerHTML = `
                    <div class="layer-content sticker-layer-content">${layer.emoji}</div>
                    <div class="layer-handles">
                        <div class="handle handle-rotate" data-handle="rotate"></div>
                        <div class="handle handle-resize handle-br" data-handle="resize-br"></div>
                    </div>
                `;
            } else {
                element.innerHTML = `
                    <div class="layer-content sticker-layer-content">
                        <img src="${layer.src}" draggable="false">
                    </div>
                    <div class="layer-handles">
                        <div class="handle handle-rotate" data-handle="rotate"></div>
                        <div class="handle handle-resize handle-br" data-handle="resize-br"></div>
                    </div>
                `;
            }
            this.applyStickerStyles(element, layer);
        }

        // Posicionar
        element.style.left = `${layer.x}px`;
        element.style.top = `${layer.y}px`;
        element.style.transform = `translate(-50%, -50%) rotate(${layer.rotation}deg) scale(${layer.scale})`;
        element.style.opacity = layer.opacity;

        // Eventos
        this.bindLayerEvents(element, layer);

        this.elementsContainer.appendChild(element);
        layer.element = element;
    }

    /**
     * Aplica estilos a una capa de texto
     */
    applyTextStyles(element, layer) {
        const content = element.querySelector('.layer-content');
        content.style.fontSize = `${layer.fontSize}px`;
        content.style.fontFamily = layer.fontFamily;
        content.style.fontWeight = layer.fontWeight;
        content.style.color = layer.color;
        content.style.textAlign = layer.textAlign;

        if (layer.backgroundColor !== 'transparent') {
            content.style.backgroundColor = layer.backgroundColor;
            content.style.padding = '8px 16px';
            content.style.borderRadius = '8px';
        }

        if (layer.shadow) {
            content.style.textShadow = '2px 2px 4px rgba(0,0,0,0.5)';
        }
    }

    /**
     * Aplica estilos a una capa de sticker
     */
    applyStickerStyles(element, layer) {
        const content = element.querySelector('.layer-content');
        content.style.fontSize = `${layer.width}px`;

        const img = content.querySelector('img');
        if (img) {
            img.style.width = `${layer.width}px`;
            img.style.height = `${layer.height}px`;
        }
    }

    /**
     * Bind eventos a un elemento de capa
     */
    bindLayerEvents(element, layer) {
        // Seleccionar al hacer click
        element.addEventListener('mousedown', (e) => this.handleMouseDown(e, layer));
        element.addEventListener('touchstart', (e) => this.handleTouchStart(e, layer), { passive: false });

        // Doble click para editar texto
        if (layer.type === 'text') {
            element.addEventListener('dblclick', () => this.enableTextEditing(layer));
        }

        // Handles
        element.querySelectorAll('.handle').forEach(handle => {
            handle.addEventListener('mousedown', (e) => this.handleHandleMouseDown(e, layer, handle.dataset.handle));
            handle.addEventListener('touchstart', (e) => this.handleHandleTouchStart(e, layer, handle.dataset.handle), { passive: false });
        });
    }

    /**
     * Mouse down en capa
     */
    handleMouseDown(e, layer) {
        if (e.target.classList.contains('handle')) return;

        e.preventDefault();
        e.stopPropagation();

        this.selectLayer(layer);
        this.isDragging = true;

        const rect = layer.element.getBoundingClientRect();
        this.dragOffset = {
            x: e.clientX - rect.left - rect.width / 2,
            y: e.clientY - rect.top - rect.height / 2
        };

        document.addEventListener('mousemove', this.handleMouseMove);
        document.addEventListener('mouseup', this.handleMouseUp);
    }

    /**
     * Touch start en capa
     */
    handleTouchStart(e, layer) {
        if (e.target.classList.contains('handle')) return;

        e.preventDefault();
        e.stopPropagation();

        this.selectLayer(layer);
        this.isDragging = true;

        const touch = e.touches[0];
        const rect = layer.element.getBoundingClientRect();
        this.dragOffset = {
            x: touch.clientX - rect.left - rect.width / 2,
            y: touch.clientY - rect.top - rect.height / 2
        };

        document.addEventListener('touchmove', this.handleTouchMove, { passive: false });
        document.addEventListener('touchend', this.handleTouchEnd);
    }

    handleMouseMove = (e) => {
        if (!this.isDragging || !this.selectedLayer) return;

        const containerRect = this.container.getBoundingClientRect();
        const x = e.clientX - containerRect.left - this.dragOffset.x;
        const y = e.clientY - containerRect.top - this.dragOffset.y;

        this.updateLayerPosition(this.selectedLayer, x, y);
    }

    handleTouchMove = (e) => {
        if (!this.isDragging || !this.selectedLayer) return;

        e.preventDefault();
        const touch = e.touches[0];
        const containerRect = this.container.getBoundingClientRect();
        const x = touch.clientX - containerRect.left - this.dragOffset.x;
        const y = touch.clientY - containerRect.top - this.dragOffset.y;

        this.updateLayerPosition(this.selectedLayer, x, y);
    }

    handleMouseUp = () => {
        this.isDragging = false;
        this.isResizing = false;
        document.removeEventListener('mousemove', this.handleMouseMove);
        document.removeEventListener('mouseup', this.handleMouseUp);
        document.removeEventListener('mousemove', this.handleResizeMove);
    }

    handleTouchEnd = () => {
        this.isDragging = false;
        this.isResizing = false;
        document.removeEventListener('touchmove', this.handleTouchMove);
        document.removeEventListener('touchend', this.handleTouchEnd);
        document.removeEventListener('touchmove', this.handleResizeTouchMove);
    }

    /**
     * Handle para resize/rotate
     */
    handleHandleMouseDown(e, layer, handleType) {
        e.preventDefault();
        e.stopPropagation();

        this.selectLayer(layer);
        this.isResizing = true;
        this.resizeHandle = handleType;
        this.resizeStart = {
            x: e.clientX,
            y: e.clientY,
            scale: layer.scale,
            rotation: layer.rotation
        };

        document.addEventListener('mousemove', this.handleResizeMove);
        document.addEventListener('mouseup', this.handleMouseUp);
    }

    handleHandleTouchStart(e, layer, handleType) {
        e.preventDefault();
        e.stopPropagation();

        const touch = e.touches[0];
        this.selectLayer(layer);
        this.isResizing = true;
        this.resizeHandle = handleType;
        this.resizeStart = {
            x: touch.clientX,
            y: touch.clientY,
            scale: layer.scale,
            rotation: layer.rotation
        };

        document.addEventListener('touchmove', this.handleResizeTouchMove, { passive: false });
        document.addEventListener('touchend', this.handleTouchEnd);
    }

    handleResizeMove = (e) => {
        if (!this.isResizing || !this.selectedLayer) return;

        if (this.resizeHandle === 'rotate') {
            this.handleRotate(e.clientX, e.clientY);
        } else {
            this.handleResize(e.clientX, e.clientY);
        }
    }

    handleResizeTouchMove = (e) => {
        if (!this.isResizing || !this.selectedLayer) return;

        e.preventDefault();
        const touch = e.touches[0];

        if (this.resizeHandle === 'rotate') {
            this.handleRotate(touch.clientX, touch.clientY);
        } else {
            this.handleResize(touch.clientX, touch.clientY);
        }
    }

    handleRotate(clientX, clientY) {
        const layer = this.selectedLayer;
        const rect = layer.element.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;

        const angle = Math.atan2(clientY - centerY, clientX - centerX) * (180 / Math.PI);
        layer.rotation = angle + 90;

        this.updateLayerTransform(layer);
    }

    handleResize(clientX, clientY) {
        const layer = this.selectedLayer;
        const dx = clientX - this.resizeStart.x;
        const dy = clientY - this.resizeStart.y;
        const distance = Math.sqrt(dx * dx + dy * dy);
        const direction = (dx + dy) > 0 ? 1 : -1;

        layer.scale = Math.max(0.3, Math.min(3, this.resizeStart.scale + (direction * distance * 0.01)));

        this.updateLayerTransform(layer);
    }

    /**
     * Actualiza posición de capa
     */
    updateLayerPosition(layer, x, y) {
        layer.x = x;
        layer.y = y;
        layer.element.style.left = `${x}px`;
        layer.element.style.top = `${y}px`;
    }

    /**
     * Actualiza transform de capa
     */
    updateLayerTransform(layer) {
        layer.element.style.transform = `translate(-50%, -50%) rotate(${layer.rotation}deg) scale(${layer.scale})`;
    }

    /**
     * Selecciona una capa
     */
    selectLayer(layer) {
        this.deselectAll();
        this.selectedLayer = layer;
        layer.element.classList.add('selected');
    }

    /**
     * Deselecciona todas las capas
     */
    deselectAll() {
        this.layers.forEach(layer => {
            layer.element.classList.remove('selected');
            // Desactivar edición de texto
            const content = layer.element.querySelector('.layer-content');
            if (content) {
                content.contentEditable = 'false';
                content.blur();
            }
        });
        this.selectedLayer = null;
    }

    /**
     * Habilita edición de texto
     */
    enableTextEditing(layer) {
        const content = layer.element.querySelector('.layer-content');
        content.contentEditable = 'true';
        content.focus();

        // Seleccionar todo el texto
        const range = document.createRange();
        range.selectNodeContents(content);
        const sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);

        // Actualizar texto al terminar
        content.addEventListener('blur', () => {
            layer.text = content.textContent;
            content.contentEditable = 'false';
        }, { once: true });
    }

    /**
     * Actualiza propiedades del texto seleccionado
     */
    updateTextProperties(properties) {
        if (!this.selectedLayer || this.selectedLayer.type !== 'text') return;

        Object.assign(this.selectedLayer, properties);
        this.applyTextStyles(this.selectedLayer.element, this.selectedLayer);
    }

    /**
     * Elimina la capa seleccionada
     */
    deleteSelectedLayer() {
        if (!this.selectedLayer) return;

        const index = this.layers.indexOf(this.selectedLayer);
        if (index > -1) {
            this.selectedLayer.element.remove();
            this.layers.splice(index, 1);
            this.selectedLayer = null;
        }
    }

    /**
     * Renderiza todas las capas en el canvas (para exportación)
     */
    renderToCanvas(targetCanvas) {
        const ctx = targetCanvas.getContext('2d');

        this.layers.forEach(layer => {
            ctx.save();

            // Calcular escala del canvas de destino
            const scaleX = targetCanvas.width / this.canvas.width;
            const scaleY = targetCanvas.height / this.canvas.height;

            ctx.translate(layer.x * scaleX, layer.y * scaleY);
            ctx.rotate(layer.rotation * Math.PI / 180);
            ctx.scale(layer.scale, layer.scale);
            ctx.globalAlpha = layer.opacity;

            if (layer.type === 'text') {
                ctx.font = `${layer.fontWeight} ${layer.fontSize * scaleX}px ${layer.fontFamily}`;
                ctx.fillStyle = layer.color;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';

                if (layer.shadow) {
                    ctx.shadowColor = 'rgba(0,0,0,0.5)';
                    ctx.shadowBlur = 4;
                    ctx.shadowOffsetX = 2;
                    ctx.shadowOffsetY = 2;
                }

                if (layer.backgroundColor !== 'transparent') {
                    const metrics = ctx.measureText(layer.text);
                    const padding = 16 * scaleX;
                    ctx.fillStyle = layer.backgroundColor;
                    ctx.fillRect(
                        -metrics.width / 2 - padding,
                        -layer.fontSize * scaleX / 2 - padding / 2,
                        metrics.width + padding * 2,
                        layer.fontSize * scaleX + padding
                    );
                    ctx.fillStyle = layer.color;
                }

                ctx.fillText(layer.text, 0, 0);
            } else if (layer.type === 'sticker') {
                if (layer.emoji) {
                    ctx.font = `${layer.width * scaleX}px Arial`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(layer.emoji, 0, 0);
                } else if (layer.src) {
                    // Para imágenes, necesitamos cargarlas
                    const img = layer.element.querySelector('img');
                    if (img && img.complete) {
                        ctx.drawImage(
                            img,
                            -layer.width * scaleX / 2,
                            -layer.height * scaleY / 2,
                            layer.width * scaleX,
                            layer.height * scaleY
                        );
                    }
                }
            }

            ctx.restore();
        });
    }

    /**
     * Obtiene datos de las capas para guardar
     */
    getLayersData() {
        return this.layers.map(layer => {
            const data = { ...layer };
            delete data.element;
            return data;
        });
    }

    /**
     * Limpia todas las capas
     */
    clear() {
        this.layers.forEach(layer => layer.element.remove());
        this.layers = [];
        this.selectedLayer = null;
    }

    /**
     * Destructor
     */
    destroy() {
        this.clear();
        this.canvas.remove();
        this.elementsContainer.remove();
    }
}
