/**
 * Admin/Supervisor Keyboard Shortcuts
 * Sistema de atajos de teclado para supervisores
 */

const AdminShortcuts = {
    shortcuts: {},
    enabled: true,
    helpModal: null,

    init: function() {
        this.registerDefaultShortcuts();
        this.attachEventListeners();
        this.createHelpModal();
        console.log('[AdminShortcuts] Inicializado');
    },

    registerDefaultShortcuts: function() {
        // Navegación
        this.register('g d', 'Ir a Dashboard', () => window.location.href = '/Admin/Dashboard');
        this.register('g u', 'Ir a Usuarios', () => window.location.href = '/Admin/Usuarios');
        this.register('g c', 'Ir a Contenido', () => window.location.href = '/Admin/Contenido');
        this.register('g r', 'Ir a Reportes', () => window.location.href = '/Admin/Reportes');
        this.register('g a', 'Ir a Apelaciones', () => window.location.href = '/Admin/Apelaciones');
        this.register('g m', 'Cola de Moderación', () => window.location.href = '/Supervisor/ColaPendiente');
        this.register('g t', 'Ir a Templates', () => window.location.href = '/Admin/Templates');
        this.register('g l', 'Ir a Logs', () => window.location.href = '/Admin/Logs');
        this.register('g s', 'Salud del Sistema', () => window.location.href = '/Admin/SaludSistema');

        // Acciones rápidas
        this.register('/', 'Abrir búsqueda global', () => this.abrirBusquedaGlobal());
        this.register('?', 'Mostrar ayuda de atajos', () => this.mostrarAyuda());
        this.register('Escape', 'Cerrar modal/popup', () => this.cerrarModales());
        this.register('r', 'Refrescar datos', () => location.reload());

        // Moderación (cuando esté en contexto)
        this.register('a', 'Aprobar (en contexto)', () => this.accionContextual('aprobar'));
        this.register('x', 'Rechazar (en contexto)', () => this.accionContextual('rechazar'));
        this.register('s', 'Saltar (en contexto)', () => this.accionContextual('saltar'));
        this.register('n', 'Siguiente item', () => this.navegarItem('siguiente'));
        this.register('p', 'Item anterior', () => this.navegarItem('anterior'));

        // Navegación numérica
        for (let i = 1; i <= 9; i++) {
            this.register(i.toString(), `Seleccionar opción ${i}`, () => this.seleccionarOpcion(i));
        }
    },

    register: function(shortcut, description, callback) {
        this.shortcuts[shortcut] = {
            description: description,
            callback: callback
        };
    },

    attachEventListeners: function() {
        let keySequence = '';
        let keyTimeout = null;

        document.addEventListener('keydown', (e) => {
            // Ignorar si está escribiendo en un input
            if (this.estaEscribiendo(e.target)) return;
            if (!this.enabled) return;

            const key = this.getKeyString(e);

            // Limpiar secuencia después de timeout
            clearTimeout(keyTimeout);
            keyTimeout = setTimeout(() => {
                keySequence = '';
            }, 1000);

            // Construir secuencia
            keySequence = keySequence ? `${keySequence} ${key}` : key;

            // Buscar atajo
            if (this.shortcuts[keySequence]) {
                e.preventDefault();
                this.shortcuts[keySequence].callback();
                keySequence = '';
            } else if (this.shortcuts[key]) {
                e.preventDefault();
                this.shortcuts[key].callback();
                keySequence = '';
            }
        });
    },

    getKeyString: function(e) {
        let key = e.key;

        // Normalizar teclas especiales
        if (key === ' ') key = 'Space';
        if (e.shiftKey && key === '/') key = '?';

        return key.toLowerCase();
    },

    estaEscribiendo: function(target) {
        const tagName = target.tagName.toLowerCase();
        const type = target.type?.toLowerCase();

        return (
            tagName === 'input' ||
            tagName === 'textarea' ||
            tagName === 'select' ||
            target.isContentEditable
        );
    },

    abrirBusquedaGlobal: function() {
        const searchInput = document.getElementById('busquedaGlobal') ||
                          document.querySelector('[data-search-global]') ||
                          document.querySelector('input[type="search"]');

        if (searchInput) {
            searchInput.focus();
            searchInput.select();
        } else {
            // Crear modal de búsqueda si no existe
            this.mostrarBusquedaRapida();
        }
    },

    mostrarBusquedaRapida: function() {
        let modal = document.getElementById('busquedaRapidaModal');
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'busquedaRapidaModal';
            modal.className = 'modal fade';
            modal.innerHTML = `
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-body p-4">
                            <div class="input-group input-group-lg">
                                <span class="input-group-text bg-white"><i class="bi bi-search"></i></span>
                                <input type="text" id="inputBusquedaRapida" class="form-control" placeholder="Buscar usuarios, contenido, reportes...">
                            </div>
                            <div id="resultadosBusquedaRapida" class="mt-3"></div>
                        </div>
                    </div>
                </div>
            `;
            document.body.appendChild(modal);

            // Evento de búsqueda
            const input = modal.querySelector('#inputBusquedaRapida');
            let searchTimeout;
            input.addEventListener('input', function() {
                clearTimeout(searchTimeout);
                searchTimeout = setTimeout(() => AdminShortcuts.buscarGlobal(this.value), 300);
            });
        }

        const bsModal = new bootstrap.Modal(modal);
        bsModal.show();
        setTimeout(() => modal.querySelector('#inputBusquedaRapida').focus(), 200);
    },

    buscarGlobal: async function(termino) {
        if (termino.length < 2) {
            document.getElementById('resultadosBusquedaRapida').innerHTML = '';
            return;
        }

        try {
            const response = await fetch(`/Admin/BusquedaGlobal?q=${encodeURIComponent(termino)}`);
            const data = await response.json();

            if (data.success) {
                this.renderResultadosBusqueda(data.resultados);
            }
        } catch (error) {
            console.error('Error en búsqueda:', error);
        }
    },

    renderResultadosBusqueda: function(resultados) {
        const container = document.getElementById('resultadosBusquedaRapida');
        let html = '';

        const categorias = [
            { key: 'usuarios', titulo: 'Usuarios', icono: 'bi-person' },
            { key: 'contenidos', titulo: 'Contenidos', icono: 'bi-images' },
            { key: 'reportes', titulo: 'Reportes', icono: 'bi-flag' },
            { key: 'apelaciones', titulo: 'Apelaciones', icono: 'bi-chat-left-text' },
            { key: 'transacciones', titulo: 'Transacciones', icono: 'bi-cash' }
        ];

        for (const cat of categorias) {
            if (resultados[cat.key]?.length > 0) {
                html += `<h6 class="text-muted small mt-3"><i class="${cat.icono} me-1"></i>${cat.titulo}</h6>`;
                for (const item of resultados[cat.key].slice(0, 5)) {
                    html += `
                        <a href="${item.url}" class="d-block p-2 text-decoration-none border-bottom result-item">
                            <div class="d-flex justify-content-between">
                                <span>${item.titulo}</span>
                                <small class="text-muted">${item.subtitulo}</small>
                            </div>
                        </a>
                    `;
                }
            }
        }

        if (!html) {
            html = '<p class="text-muted text-center py-3">No se encontraron resultados</p>';
        }

        container.innerHTML = html;
    },

    cerrarModales: function() {
        const modales = document.querySelectorAll('.modal.show');
        modales.forEach(modal => {
            const bsModal = bootstrap.Modal.getInstance(modal);
            if (bsModal) bsModal.hide();
        });
    },

    accionContextual: function(accion) {
        // Buscar botón de acción en la página
        const selector = {
            'aprobar': '[data-action="aprobar"], .btn-aprobar, [onclick*="aprobar"]',
            'rechazar': '[data-action="rechazar"], .btn-rechazar, [onclick*="rechazar"]',
            'saltar': '[data-action="saltar"], .btn-saltar, [onclick*="saltar"]'
        };

        const btn = document.querySelector(selector[accion]);
        if (btn) btn.click();
    },

    navegarItem: function(direccion) {
        const items = document.querySelectorAll('[data-item], .item-lista, .card[data-id]');
        const activo = document.querySelector('.item-activo, [data-item].active');
        let index = Array.from(items).indexOf(activo);

        if (direccion === 'siguiente') {
            index = (index + 1) % items.length;
        } else {
            index = index <= 0 ? items.length - 1 : index - 1;
        }

        items.forEach(i => i.classList.remove('item-activo', 'active'));
        if (items[index]) {
            items[index].classList.add('item-activo', 'active');
            items[index].scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    },

    seleccionarOpcion: function(numero) {
        const opciones = document.querySelectorAll('[data-option], .opcion-lista');
        if (opciones[numero - 1]) {
            opciones[numero - 1].click();
        }
    },

    createHelpModal: function() {
        const modal = document.createElement('div');
        modal.id = 'shortcutsHelpModal';
        modal.className = 'modal fade';
        modal.innerHTML = `
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title"><i class="bi bi-keyboard me-2"></i>Atajos de Teclado</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="row" id="shortcutsHelpContent"></div>
                    </div>
                </div>
            </div>
        `;
        document.body.appendChild(modal);
        this.helpModal = new bootstrap.Modal(modal);
    },

    mostrarAyuda: function() {
        const container = document.getElementById('shortcutsHelpContent');

        // Agrupar atajos por categoría
        const grupos = {
            'Navegación': ['g d', 'g u', 'g c', 'g r', 'g a', 'g m', 'g t', 'g l', 'g s'],
            'Acciones': ['/', '?', 'escape', 'r'],
            'Moderación': ['a', 'x', 's', 'n', 'p']
        };

        let html = '';
        for (const [grupo, keys] of Object.entries(grupos)) {
            html += `<div class="col-md-6 mb-3">
                <h6 class="text-primary border-bottom pb-2">${grupo}</h6>
                <div class="list-group list-group-flush">`;

            for (const key of keys) {
                if (this.shortcuts[key]) {
                    html += `
                        <div class="list-group-item d-flex justify-content-between align-items-center px-0 py-2">
                            <span>${this.shortcuts[key].description}</span>
                            <kbd class="bg-dark text-white px-2 py-1 rounded">${key}</kbd>
                        </div>
                    `;
                }
            }

            html += '</div></div>';
        }

        container.innerHTML = html;
        this.helpModal.show();
    },

    toggle: function() {
        this.enabled = !this.enabled;
        console.log('[AdminShortcuts]', this.enabled ? 'Habilitados' : 'Deshabilitados');
    }
};

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', () => AdminShortcuts.init());

// Exportar para uso global
window.AdminShortcuts = AdminShortcuts;
