/**
 * TransformTools.js - Herramientas de transformación para ReelsCreator
 * Incluye: Recortar (Crop), Voltear (Flip), Rotar
 */

class TransformTools {
    constructor(mediaContainer) {
        this.container = mediaContainer;
        this.panel = null;
        this.isVisible = false;

        // Estado de transformación
        this.flipH = false;
        this.flipV = false;
        this.rotation = 0;

        // Crop state
        this.isCropping = false;
        this.cropOverlay = null;
        this.cropRegion = { x: 0, y: 0, width: 100, height: 100 }; // porcentajes
        this.aspectRatio = null; // null = libre

        // Aspect ratios predefinidos
        this.aspectRatios = [
            { id: 'free', name: 'Libre', ratio: null, icon: '⬜' },
            { id: '1:1', name: '1:1', ratio: 1, icon: '🔲' },
            { id: '4:5', name: '4:5', ratio: 4/5, icon: '📱' },
            { id: '9:16', name: '9:16', ratio: 9/16, icon: '📲' },
            { id: '16:9', name: '16:9', ratio: 16/9, icon: '🖥️' },
            { id: '4:3', name: '4:3', ratio: 4/3, icon: '📺' }
        ];

        this.init();
    }

    init() {
        this.createPanel();
    }

    createPanel() {
        this.panel = document.createElement('div');
        this.panel.className = 'transform-tools-panel editor-panel';
        this.panel.innerHTML = `
            <div class="panel-header transform-panel-header">
                <h3>✂️ Transformar</h3>
                <button class="panel-close-btn" id="transformPanelClose">&times;</button>
            </div>

            <div class="panel-content">
                <!-- Sección de Voltear -->
                <div class="panel-section">
                    <h4 class="section-title">Voltear</h4>
                    <div class="flip-buttons">
                        <button class="transform-btn" id="flipHorizontal" title="Voltear Horizontal">
                            <span class="transform-icon">↔️</span>
                            <span class="transform-label">Horizontal</span>
                        </button>
                        <button class="transform-btn" id="flipVertical" title="Voltear Vertical">
                            <span class="transform-icon">↕️</span>
                            <span class="transform-label">Vertical</span>
                        </button>
                    </div>
                </div>

                <!-- Sección de Rotar -->
                <div class="panel-section">
                    <h4 class="section-title">Rotar</h4>
                    <div class="rotate-buttons">
                        <button class="transform-btn" id="rotateLeft" title="Rotar 90° izquierda">
                            <span class="transform-icon">↪️</span>
                            <span class="transform-label">-90°</span>
                        </button>
                        <button class="transform-btn" id="rotateRight" title="Rotar 90° derecha">
                            <span class="transform-icon">↩️</span>
                            <span class="transform-label">+90°</span>
                        </button>
                    </div>
                </div>

                <!-- Sección de Recortar -->
                <div class="panel-section">
                    <h4 class="section-title">Recortar</h4>
                    <div class="aspect-ratio-grid" id="aspectRatioGrid">
                        ${this.renderAspectRatioButtons()}
                    </div>
                    <div class="crop-actions">
                        <button class="crop-start-btn" id="startCrop">
                            <span>✂️</span> Iniciar Recorte
                        </button>
                        <button class="crop-apply-btn" id="applyCrop" style="display: none;">
                            <span>✓</span> Aplicar Recorte
                        </button>
                        <button class="crop-cancel-btn" id="cancelCrop" style="display: none;">
                            <span>✕</span> Cancelar
                        </button>
                    </div>
                </div>

                <!-- Sección de Reset -->
                <div class="panel-section reset-section">
                    <button class="reset-transforms-btn" id="resetTransforms">
                        <span>🔄</span> Restablecer Todo
                    </button>
                </div>
            </div>
        `;

        this.bindPanelEvents();
    }

    renderAspectRatioButtons() {
        return this.aspectRatios.map(ar => `
            <button class="aspect-btn ${ar.id === 'free' ? 'active' : ''}" data-ratio="${ar.id}" title="${ar.name}">
                <span class="aspect-icon">${ar.icon}</span>
                <span class="aspect-name">${ar.name}</span>
            </button>
        `).join('');
    }

    bindPanelEvents() {
        // Close button
        this.panel.querySelector('#transformPanelClose').addEventListener('click', () => this.hide());

        // Flip buttons
        this.panel.querySelector('#flipHorizontal').addEventListener('click', () => this.flipHorizontal());
        this.panel.querySelector('#flipVertical').addEventListener('click', () => this.flipVertical());

        // Rotate buttons
        this.panel.querySelector('#rotateLeft').addEventListener('click', () => this.rotate(-90));
        this.panel.querySelector('#rotateRight').addEventListener('click', () => this.rotate(90));

        // Aspect ratio buttons
        this.panel.querySelectorAll('.aspect-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.aspect-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                const ratioId = btn.dataset.ratio;
                const ar = this.aspectRatios.find(a => a.id === ratioId);
                this.aspectRatio = ar ? ar.ratio : null;
                if (this.isCropping) {
                    this.updateCropRegion();
                }
            });
        });

        // Crop buttons
        this.panel.querySelector('#startCrop').addEventListener('click', () => this.startCrop());
        this.panel.querySelector('#applyCrop').addEventListener('click', () => this.applyCrop());
        this.panel.querySelector('#cancelCrop').addEventListener('click', () => this.cancelCrop());

        // Reset button
        this.panel.querySelector('#resetTransforms').addEventListener('click', () => this.resetAll());
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
        if (this.isCropping) {
            this.cancelCrop();
        }
    }

    toggle(container) {
        if (this.isVisible) {
            this.hide();
        } else {
            this.show(container);
        }
    }

    getMediaElement() {
        return document.getElementById('editorMedia');
    }

    flipHorizontal() {
        this.flipH = !this.flipH;
        this.applyTransform();
        this.panel.querySelector('#flipHorizontal').classList.toggle('active', this.flipH);
        this.showNotification(this.flipH ? 'Volteado horizontal' : 'Volteo horizontal quitado');
    }

    flipVertical() {
        this.flipV = !this.flipV;
        this.applyTransform();
        this.panel.querySelector('#flipVertical').classList.toggle('active', this.flipV);
        this.showNotification(this.flipV ? 'Volteado vertical' : 'Volteo vertical quitado');
    }

    rotate(degrees) {
        this.rotation = (this.rotation + degrees) % 360;
        if (this.rotation < 0) this.rotation += 360;
        this.applyTransform();
        this.showNotification(`Rotado ${this.rotation}°`);
    }

    applyTransform() {
        const media = this.getMediaElement();
        if (!media) return;

        const transforms = [];

        if (this.rotation !== 0) {
            transforms.push(`rotate(${this.rotation}deg)`);
        }
        if (this.flipH) {
            transforms.push('scaleX(-1)');
        }
        if (this.flipV) {
            transforms.push('scaleY(-1)');
        }

        media.style.transform = transforms.length > 0 ? transforms.join(' ') : '';

        // Ajustar el contenedor si hay rotación de 90 o 270 grados
        if (this.rotation === 90 || this.rotation === 270) {
            media.style.maxWidth = '100vh';
            media.style.maxHeight = '100vw';
        } else {
            media.style.maxWidth = '';
            media.style.maxHeight = '';
        }
    }

    startCrop() {
        const media = this.getMediaElement();
        if (!media) return;

        this.isCropping = true;

        // Mostrar botones de aplicar/cancelar
        this.panel.querySelector('#startCrop').style.display = 'none';
        this.panel.querySelector('#applyCrop').style.display = 'inline-flex';
        this.panel.querySelector('#cancelCrop').style.display = 'inline-flex';

        // Crear overlay de recorte
        this.createCropOverlay();

        this.showNotification('Arrastra para ajustar el recorte');
    }

    createCropOverlay() {
        const media = this.getMediaElement();
        if (!media) return;

        const mediaContainer = media.parentElement;

        // Crear overlay
        this.cropOverlay = document.createElement('div');
        this.cropOverlay.className = 'crop-overlay';
        this.cropOverlay.innerHTML = `
            <div class="crop-dark-area crop-dark-top"></div>
            <div class="crop-dark-area crop-dark-bottom"></div>
            <div class="crop-dark-area crop-dark-left"></div>
            <div class="crop-dark-area crop-dark-right"></div>
            <div class="crop-region" id="cropRegion">
                <div class="crop-handle crop-handle-nw" data-handle="nw"></div>
                <div class="crop-handle crop-handle-ne" data-handle="ne"></div>
                <div class="crop-handle crop-handle-sw" data-handle="sw"></div>
                <div class="crop-handle crop-handle-se" data-handle="se"></div>
                <div class="crop-handle crop-handle-n" data-handle="n"></div>
                <div class="crop-handle crop-handle-s" data-handle="s"></div>
                <div class="crop-handle crop-handle-e" data-handle="e"></div>
                <div class="crop-handle crop-handle-w" data-handle="w"></div>
                <div class="crop-grid">
                    <div class="crop-grid-line crop-grid-h1"></div>
                    <div class="crop-grid-line crop-grid-h2"></div>
                    <div class="crop-grid-line crop-grid-v1"></div>
                    <div class="crop-grid-line crop-grid-v2"></div>
                </div>
            </div>
        `;

        mediaContainer.appendChild(this.cropOverlay);

        // Inicializar región de recorte al 80% centrado
        this.cropRegion = { x: 10, y: 10, width: 80, height: 80 };
        this.updateCropDisplay();

        // Bind eventos de drag
        this.bindCropEvents();
    }

    bindCropEvents() {
        const cropRegion = this.cropOverlay.querySelector('#cropRegion');
        let isDragging = false;
        let isResizing = false;
        let currentHandle = null;
        let startX, startY;
        let startRegion = {};

        const startDrag = (e) => {
            if (e.target.classList.contains('crop-handle')) {
                isResizing = true;
                currentHandle = e.target.dataset.handle;
            } else if (e.target.id === 'cropRegion' || e.target.closest('#cropRegion')) {
                isDragging = true;
            }

            if (isDragging || isResizing) {
                const touch = e.touches ? e.touches[0] : e;
                startX = touch.clientX;
                startY = touch.clientY;
                startRegion = { ...this.cropRegion };
                e.preventDefault();
            }
        };

        const doDrag = (e) => {
            if (!isDragging && !isResizing) return;

            const touch = e.touches ? e.touches[0] : e;
            const rect = this.cropOverlay.getBoundingClientRect();
            const deltaX = ((touch.clientX - startX) / rect.width) * 100;
            const deltaY = ((touch.clientY - startY) / rect.height) * 100;

            if (isDragging) {
                // Mover región
                let newX = startRegion.x + deltaX;
                let newY = startRegion.y + deltaY;

                // Limitar a los bordes
                newX = Math.max(0, Math.min(100 - startRegion.width, newX));
                newY = Math.max(0, Math.min(100 - startRegion.height, newY));

                this.cropRegion.x = newX;
                this.cropRegion.y = newY;
            } else if (isResizing) {
                // Redimensionar región
                this.resizeCropRegion(currentHandle, deltaX, deltaY, startRegion);
            }

            this.updateCropDisplay();
            e.preventDefault();
        };

        const stopDrag = () => {
            isDragging = false;
            isResizing = false;
            currentHandle = null;
        };

        cropRegion.addEventListener('mousedown', startDrag);
        cropRegion.addEventListener('touchstart', startDrag, { passive: false });
        document.addEventListener('mousemove', doDrag);
        document.addEventListener('touchmove', doDrag, { passive: false });
        document.addEventListener('mouseup', stopDrag);
        document.addEventListener('touchend', stopDrag);

        // Guardar referencias para limpieza
        this._cropEventCleanup = () => {
            document.removeEventListener('mousemove', doDrag);
            document.removeEventListener('touchmove', doDrag);
            document.removeEventListener('mouseup', stopDrag);
            document.removeEventListener('touchend', stopDrag);
        };
    }

    resizeCropRegion(handle, deltaX, deltaY, startRegion) {
        const minSize = 10; // mínimo 10%

        let newRegion = { ...startRegion };

        switch (handle) {
            case 'nw':
                newRegion.x = Math.max(0, startRegion.x + deltaX);
                newRegion.y = Math.max(0, startRegion.y + deltaY);
                newRegion.width = Math.max(minSize, startRegion.width - deltaX);
                newRegion.height = Math.max(minSize, startRegion.height - deltaY);
                break;
            case 'ne':
                newRegion.y = Math.max(0, startRegion.y + deltaY);
                newRegion.width = Math.max(minSize, startRegion.width + deltaX);
                newRegion.height = Math.max(minSize, startRegion.height - deltaY);
                break;
            case 'sw':
                newRegion.x = Math.max(0, startRegion.x + deltaX);
                newRegion.width = Math.max(minSize, startRegion.width - deltaX);
                newRegion.height = Math.max(minSize, startRegion.height + deltaY);
                break;
            case 'se':
                newRegion.width = Math.max(minSize, startRegion.width + deltaX);
                newRegion.height = Math.max(minSize, startRegion.height + deltaY);
                break;
            case 'n':
                newRegion.y = Math.max(0, startRegion.y + deltaY);
                newRegion.height = Math.max(minSize, startRegion.height - deltaY);
                break;
            case 's':
                newRegion.height = Math.max(minSize, startRegion.height + deltaY);
                break;
            case 'e':
                newRegion.width = Math.max(minSize, startRegion.width + deltaX);
                break;
            case 'w':
                newRegion.x = Math.max(0, startRegion.x + deltaX);
                newRegion.width = Math.max(minSize, startRegion.width - deltaX);
                break;
        }

        // Aplicar aspect ratio si está definido
        if (this.aspectRatio) {
            const containerRect = this.cropOverlay.getBoundingClientRect();
            const containerAspect = containerRect.width / containerRect.height;
            const targetAspect = this.aspectRatio;

            // Ajustar altura basado en el ancho
            const pixelWidth = (newRegion.width / 100) * containerRect.width;
            const targetHeight = pixelWidth / targetAspect;
            newRegion.height = (targetHeight / containerRect.height) * 100;
        }

        // Limitar a los bordes
        newRegion.width = Math.min(100 - newRegion.x, newRegion.width);
        newRegion.height = Math.min(100 - newRegion.y, newRegion.height);

        this.cropRegion = newRegion;
    }

    updateCropRegion() {
        // Actualizar región cuando cambia el aspect ratio
        if (this.aspectRatio && this.isCropping) {
            const containerRect = this.cropOverlay.getBoundingClientRect();
            const targetAspect = this.aspectRatio;

            // Mantener el ancho, ajustar altura
            const pixelWidth = (this.cropRegion.width / 100) * containerRect.width;
            const targetHeight = pixelWidth / targetAspect;
            this.cropRegion.height = Math.min(100 - this.cropRegion.y, (targetHeight / containerRect.height) * 100);

            this.updateCropDisplay();
        }
    }

    updateCropDisplay() {
        if (!this.cropOverlay) return;

        const { x, y, width, height } = this.cropRegion;

        // Actualizar región visible
        const region = this.cropOverlay.querySelector('#cropRegion');
        region.style.left = `${x}%`;
        region.style.top = `${y}%`;
        region.style.width = `${width}%`;
        region.style.height = `${height}%`;

        // Actualizar áreas oscuras
        const darkTop = this.cropOverlay.querySelector('.crop-dark-top');
        const darkBottom = this.cropOverlay.querySelector('.crop-dark-bottom');
        const darkLeft = this.cropOverlay.querySelector('.crop-dark-left');
        const darkRight = this.cropOverlay.querySelector('.crop-dark-right');

        darkTop.style.height = `${y}%`;
        darkBottom.style.height = `${100 - y - height}%`;
        darkLeft.style.top = `${y}%`;
        darkLeft.style.height = `${height}%`;
        darkLeft.style.width = `${x}%`;
        darkRight.style.top = `${y}%`;
        darkRight.style.height = `${height}%`;
        darkRight.style.width = `${100 - x - width}%`;
    }

    async applyCrop() {
        const media = this.getMediaElement();
        if (!media || !this.isCropping) return;

        this.showNotification('Aplicando recorte...');

        try {
            // Crear canvas para el recorte
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');

            // Obtener dimensiones originales
            const isVideo = media.tagName === 'VIDEO';
            const sourceWidth = isVideo ? media.videoWidth : media.naturalWidth;
            const sourceHeight = isVideo ? media.videoHeight : media.naturalHeight;

            // Calcular región de recorte en píxeles
            const cropX = (this.cropRegion.x / 100) * sourceWidth;
            const cropY = (this.cropRegion.y / 100) * sourceHeight;
            const cropWidth = (this.cropRegion.width / 100) * sourceWidth;
            const cropHeight = (this.cropRegion.height / 100) * sourceHeight;

            // Configurar canvas
            canvas.width = cropWidth;
            canvas.height = cropHeight;

            // Aplicar transformaciones
            ctx.save();

            // Dibujar región recortada
            ctx.drawImage(media, cropX, cropY, cropWidth, cropHeight, 0, 0, cropWidth, cropHeight);

            ctx.restore();

            // Convertir a blob y actualizar media
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.92));
            const url = URL.createObjectURL(blob);

            // Actualizar imagen
            if (!isVideo) {
                media.src = url;
            }

            // Guardar datos del recorte para exportación
            this.lastCropData = {
                x: cropX,
                y: cropY,
                width: cropWidth,
                height: cropHeight,
                blob: blob
            };

            this.cancelCrop();
            this.showNotification('Recorte aplicado');

        } catch (error) {
            console.error('Error aplicando recorte:', error);
            this.showNotification('Error al aplicar recorte');
        }
    }

    cancelCrop() {
        this.isCropping = false;

        // Remover overlay
        if (this.cropOverlay) {
            if (this._cropEventCleanup) {
                this._cropEventCleanup();
            }
            this.cropOverlay.remove();
            this.cropOverlay = null;
        }

        // Restaurar botones
        this.panel.querySelector('#startCrop').style.display = 'inline-flex';
        this.panel.querySelector('#applyCrop').style.display = 'none';
        this.panel.querySelector('#cancelCrop').style.display = 'none';
    }

    resetAll() {
        this.flipH = false;
        this.flipV = false;
        this.rotation = 0;
        this.aspectRatio = null;

        this.applyTransform();

        // Reset UI
        this.panel.querySelector('#flipHorizontal').classList.remove('active');
        this.panel.querySelector('#flipVertical').classList.remove('active');
        this.panel.querySelectorAll('.aspect-btn').forEach(b => b.classList.remove('active'));
        this.panel.querySelector('.aspect-btn[data-ratio="free"]').classList.add('active');

        if (this.isCropping) {
            this.cancelCrop();
        }

        this.showNotification('Transformaciones restablecidas');
    }

    showNotification(message) {
        const notification = document.createElement('div');
        notification.className = 'transform-notification';
        notification.textContent = message;
        notification.style.cssText = `
            position: fixed;
            bottom: 100px;
            left: 50%;
            transform: translateX(-50%);
            background: rgba(0,0,0,0.85);
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
     * Obtiene los datos de transformación para exportación
     */
    getTransformData() {
        return {
            flipH: this.flipH,
            flipV: this.flipV,
            rotation: this.rotation,
            crop: this.lastCropData || null
        };
    }

    /**
     * Aplica transformaciones a un canvas para exportación
     */
    applyToCanvas(canvas, sourceMedia) {
        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;

        ctx.save();

        // Mover al centro
        ctx.translate(width / 2, height / 2);

        // Aplicar rotación
        if (this.rotation !== 0) {
            ctx.rotate((this.rotation * Math.PI) / 180);
        }

        // Aplicar flip
        const scaleX = this.flipH ? -1 : 1;
        const scaleY = this.flipV ? -1 : 1;
        ctx.scale(scaleX, scaleY);

        // Dibujar imagen centrada
        ctx.drawImage(sourceMedia, -width / 2, -height / 2, width, height);

        ctx.restore();
    }

    reset() {
        this.resetAll();
    }

    destroy() {
        this.reset();
        if (this.panel && this.panel.parentElement) {
            this.panel.parentElement.removeChild(this.panel);
        }
    }
}

// Export global
window.TransformTools = TransformTools;
