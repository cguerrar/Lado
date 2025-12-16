/**
 * StickerLayer.js - Galería de Stickers
 * Panel de stickers y emojis
 */

class StickerLayer {
    constructor(layerManager) {
        this.layerManager = layerManager;
        this.panel = null;
        this.isVisible = false;
        this.currentCategory = 'emojis';

        // Categorías de emojis
        this.emojiCategories = {
            emojis: {
                name: 'Emojis',
                icon: '😀',
                items: [
                    '😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂',
                    '🙂', '😉', '😊', '😇', '🥰', '😍', '🤩', '😘',
                    '😋', '😛', '😜', '🤪', '😝', '🤑', '🤗', '🤭',
                    '🤫', '🤔', '🤐', '🤨', '😐', '😑', '😶', '😏',
                    '😒', '🙄', '😬', '😮‍💨', '🤥', '😌', '😔', '😪',
                    '🤤', '😴', '😷', '🤒', '🤕', '🤢', '🤮', '🥵',
                    '🥶', '🥴', '😵', '🤯', '🤠', '🥳', '🥸', '😎'
                ]
            },
            love: {
                name: 'Amor',
                icon: '❤️',
                items: [
                    '❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍',
                    '🤎', '💔', '❣️', '💕', '💞', '💓', '💗', '💖',
                    '💝', '💘', '💌', '💋', '👄', '🫦', '👅', '🌹',
                    '🥀', '💐', '🌷', '🌸', '💮', '🏵️', '🌺', '🌻'
                ]
            },
            gestures: {
                name: 'Gestos',
                icon: '👋',
                items: [
                    '👋', '🤚', '🖐️', '✋', '🖖', '👌', '🤌', '🤏',
                    '✌️', '🤞', '🤟', '🤘', '🤙', '👈', '👉', '👆',
                    '🖕', '👇', '☝️', '👍', '👎', '✊', '👊', '🤛',
                    '🤜', '👏', '🙌', '👐', '🤲', '🤝', '🙏', '💪'
                ]
            },
            animals: {
                name: 'Animales',
                icon: '🐶',
                items: [
                    '🐶', '🐱', '🐭', '🐹', '🐰', '🦊', '🐻', '🐼',
                    '🐻‍❄️', '🐨', '🐯', '🦁', '🐮', '🐷', '🐸', '🐵',
                    '🐔', '🐧', '🐦', '🐤', '🦆', '🦅', '🦉', '🦇',
                    '🐺', '🐗', '🐴', '🦄', '🐝', '🪱', '🐛', '🦋'
                ]
            },
            food: {
                name: 'Comida',
                icon: '🍕',
                items: [
                    '🍎', '🍐', '🍊', '🍋', '🍌', '🍉', '🍇', '🍓',
                    '🫐', '🍈', '🍒', '🍑', '🥭', '🍍', '🥥', '🥝',
                    '🍅', '🍆', '🥑', '🌮', '🌯', '🥗', '🍕', '🍔',
                    '🍟', '🌭', '🍿', '🧁', '🍰', '🎂', '🍩', '🍪'
                ]
            },
            objects: {
                name: 'Objetos',
                icon: '⭐',
                items: [
                    '⭐', '🌟', '✨', '💫', '🔥', '💥', '💯', '💢',
                    '💨', '💦', '💤', '🎵', '🎶', '🎤', '🎧', '🎸',
                    '🎮', '🎯', '🎪', '🎭', '🎨', '🎬', '📷', '📱',
                    '💻', '⌚', '💎', '💰', '🏆', '🥇', '🎁', '🎀'
                ]
            },
            symbols: {
                name: 'Símbolos',
                icon: '💯',
                items: [
                    '✅', '❌', '❓', '❗', '💯', '🔴', '🟠', '🟡',
                    '🟢', '🔵', '🟣', '⚫', '⚪', '🟤', '🔶', '🔷',
                    '🔸', '🔹', '▪️', '▫️', '◾', '◽', '⬛', '⬜',
                    '🔳', '🔲', '🏁', '🚩', '🎌', '🏴', '🏳️', '⚡'
                ]
            },
            flags: {
                name: 'Banderas',
                icon: '🇨🇱',
                items: [
                    '🇦🇷', '🇧🇴', '🇧🇷', '🇨🇱', '🇨🇴', '🇨🇷', '🇨🇺', '🇩🇴',
                    '🇪🇨', '🇸🇻', '🇬🇹', '🇭🇳', '🇲🇽', '🇳🇮', '🇵🇦', '🇵🇾',
                    '🇵🇪', '🇵🇷', '🇺🇾', '🇻🇪', '🇪🇸', '🇺🇸', '🇬🇧', '🇫🇷',
                    '🇩🇪', '🇮🇹', '🇯🇵', '🇰🇷', '🇨🇳', '🇦🇺', '🇨🇦', '🌎'
                ]
            }
        };

        this.init();
    }

    init() {
        this.createPanel();
    }

    createPanel() {
        this.panel = document.createElement('div');
        this.panel.className = 'sticker-panel';
        this.panel.innerHTML = `
            <div class="sticker-panel-header">
                <span class="sticker-panel-title">Stickers</span>
                <button class="sticker-panel-close" id="closeStickerPanel">&times;</button>
            </div>

            <div class="sticker-categories" id="stickerCategories">
                ${Object.keys(this.emojiCategories).map(key => `
                    <button class="category-btn ${key === 'emojis' ? 'active' : ''}"
                            data-category="${key}"
                            title="${this.emojiCategories[key].name}">
                        ${this.emojiCategories[key].icon}
                    </button>
                `).join('')}
            </div>

            <div class="sticker-grid" id="stickerGrid">
                ${this.renderStickers('emojis')}
            </div>

            <div class="sticker-size-control">
                <label>Tamaño: <span id="stickerSizeValue">80</span>px</label>
                <input type="range" id="stickerSizeSlider" min="40" max="150" value="80" class="text-slider">

                <button class="delete-sticker-btn" id="deleteStickerBtn">
                    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3 6 5 6 21 6"></polyline>
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                    </svg>
                    Eliminar Seleccionado
                </button>
            </div>
        `;

        this.bindPanelEvents();
    }

    renderStickers(category) {
        const items = this.emojiCategories[category]?.items || [];
        return items.map(emoji => `
            <button class="sticker-btn" data-emoji="${emoji}">${emoji}</button>
        `).join('');
    }

    bindPanelEvents() {
        // Close panel
        this.panel.querySelector('#closeStickerPanel').addEventListener('click', () => this.hide());

        // Category buttons
        this.panel.querySelectorAll('.category-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                this.panel.querySelectorAll('.category-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this.currentCategory = btn.dataset.category;
                this.panel.querySelector('#stickerGrid').innerHTML = this.renderStickers(this.currentCategory);
                this.bindStickerButtons();
            });
        });

        // Sticker buttons
        this.bindStickerButtons();

        // Size slider
        const sizeSlider = this.panel.querySelector('#stickerSizeSlider');
        const sizeValue = this.panel.querySelector('#stickerSizeValue');
        sizeSlider.addEventListener('input', () => {
            sizeValue.textContent = sizeSlider.value;
        });

        // Delete
        this.panel.querySelector('#deleteStickerBtn').addEventListener('click', () => {
            this.layerManager.deleteSelectedLayer();
        });
    }

    bindStickerButtons() {
        this.panel.querySelectorAll('.sticker-btn').forEach(btn => {
            btn.addEventListener('click', () => this.addSticker(btn.dataset.emoji));
        });
    }

    addSticker(emoji) {
        const size = parseInt(this.panel.querySelector('#stickerSizeSlider').value);
        this.layerManager.addStickerLayer({
            emoji: emoji,
            width: size,
            height: size
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

    destroy() {
        this.panel.remove();
    }
}
