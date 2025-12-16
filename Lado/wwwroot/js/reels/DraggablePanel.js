/**
 * DraggablePanel.js - Hace los paneles arrastrables
 * Permite mover paneles por su header
 */

class DraggablePanel {
    constructor() {
        this.isDragging = false;
        this.currentPanel = null;
        this.startX = 0;
        this.startY = 0;
        this.initialLeft = 0;
        this.initialTop = 0;
        this.hasBeenDragged = new WeakMap(); // Track which panels have been dragged

        this.init();
    }

    init() {
        // Bindear eventos globales
        document.addEventListener('mousemove', (e) => this.onMouseMove(e));
        document.addEventListener('mouseup', (e) => this.onMouseUp(e));
        document.addEventListener('touchmove', (e) => this.onTouchMove(e), { passive: false });
        document.addEventListener('touchend', (e) => this.onTouchEnd(e));
    }

    /**
     * Hace un panel arrastrable por su header
     */
    makeDraggable(panel, headerSelector) {
        if (!panel) return;

        const header = panel.querySelector(headerSelector);
        if (!header) return;

        // Estilo del cursor en el header
        header.style.cursor = 'move';

        // Mouse events
        header.addEventListener('mousedown', (e) => this.onMouseDown(e, panel));

        // Touch events
        header.addEventListener('touchstart', (e) => this.onTouchStart(e, panel), { passive: false });

        // Agregar indicador visual de que es arrastrable
        this.addDragIndicator(header);
    }

    addDragIndicator(header) {
        // Agregar icono de arrastre si no existe
        if (!header.querySelector('.drag-indicator')) {
            const indicator = document.createElement('div');
            indicator.className = 'drag-indicator';
            indicator.innerHTML = `
                <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                    <circle cx="9" cy="6" r="1.5"/>
                    <circle cx="15" cy="6" r="1.5"/>
                    <circle cx="9" cy="12" r="1.5"/>
                    <circle cx="15" cy="12" r="1.5"/>
                    <circle cx="9" cy="18" r="1.5"/>
                    <circle cx="15" cy="18" r="1.5"/>
                </svg>
            `;
            header.insertBefore(indicator, header.firstChild);
        }
    }

    onMouseDown(e, panel) {
        // No arrastrar si se hace clic en un botón o input
        if (e.target.closest('button, input, select, textarea, .drag-indicator svg')) return;

        e.preventDefault();
        e.stopPropagation();
        this.startDrag(e.clientX, e.clientY, panel);
    }

    onTouchStart(e, panel) {
        if (e.target.closest('button, input, select, textarea')) return;

        e.preventDefault();
        const touch = e.touches[0];
        this.startDrag(touch.clientX, touch.clientY, panel);
    }

    startDrag(clientX, clientY, panel) {
        this.isDragging = true;
        this.currentPanel = panel;
        this.startX = clientX;
        this.startY = clientY;

        // Obtener posición visual actual del panel (getBoundingClientRect da la posición real en pantalla)
        const rect = panel.getBoundingClientRect();

        // Si es la primera vez que se arrastra, convertir a posición fixed
        if (!this.hasBeenDragged.get(panel)) {
            // Guardar la posición actual en pantalla y aplicar estilos inline
            panel.style.position = 'fixed';
            panel.style.left = rect.left + 'px';
            panel.style.top = rect.top + 'px';
            panel.style.right = 'auto';
            panel.style.bottom = 'auto';
            panel.style.transform = 'none';
            panel.style.margin = '0';

            this.hasBeenDragged.set(panel, true);
        }

        // Usar la posición actual del estilo inline (ya convertida)
        this.initialLeft = parseFloat(panel.style.left) || rect.left;
        this.initialTop = parseFloat(panel.style.top) || rect.top;

        // Agregar clase de arrastre
        panel.classList.add('dragging');
        document.body.style.userSelect = 'none';
    }

    onMouseMove(e) {
        if (!this.isDragging || !this.currentPanel) return;
        e.preventDefault();
        this.updatePosition(e.clientX, e.clientY);
    }

    onTouchMove(e) {
        if (!this.isDragging || !this.currentPanel) return;
        e.preventDefault();
        const touch = e.touches[0];
        this.updatePosition(touch.clientX, touch.clientY);
    }

    updatePosition(clientX, clientY) {
        const deltaX = clientX - this.startX;
        const deltaY = clientY - this.startY;

        let newLeft = this.initialLeft + deltaX;
        let newTop = this.initialTop + deltaY;

        // Obtener dimensiones actuales del panel
        const panelRect = this.currentPanel.getBoundingClientRect();

        // Limitar al viewport (dejar al menos 50px visible en cada lado)
        const minLeft = -panelRect.width + 50;
        const maxLeft = window.innerWidth - 50;
        const minTop = 0;
        const maxTop = window.innerHeight - 50;

        newLeft = Math.max(minLeft, Math.min(newLeft, maxLeft));
        newTop = Math.max(minTop, Math.min(newTop, maxTop));

        this.currentPanel.style.left = newLeft + 'px';
        this.currentPanel.style.top = newTop + 'px';
    }

    onMouseUp(e) {
        this.endDrag();
    }

    onTouchEnd(e) {
        this.endDrag();
    }

    endDrag() {
        if (this.currentPanel) {
            this.currentPanel.classList.remove('dragging');
        }
        this.isDragging = false;
        this.currentPanel = null;
        document.body.style.userSelect = '';
    }

    /**
     * Resetea la posición de un panel a su posición original
     */
    resetPosition(panel) {
        if (!panel) return;
        panel.style.left = '';
        panel.style.top = '';
        panel.style.right = '';
        panel.style.bottom = '';
        panel.style.position = '';
        panel.style.transform = '';
        panel.style.margin = '';
        this.hasBeenDragged.delete(panel);
    }
}

// Instancia global
window.DraggablePanel = DraggablePanel;
