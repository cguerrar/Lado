/**
 * FilterEngine.js - Motor de Filtros
 * Aplica filtros CSS y Canvas a imágenes/videos
 */

class FilterEngine {
    constructor() {
        this.currentFilter = 'normal';
        this.filterIntensity = 100;

        // Definición de filtros disponibles
        this.filters = {
            normal: {
                name: 'Normal',
                css: 'none',
                adjustments: {}
            },
            clarendon: {
                name: 'Clarendon',
                css: 'contrast(1.2) saturate(1.35)',
                adjustments: { contrast: 120, saturate: 135 }
            },
            gingham: {
                name: 'Gingham',
                css: 'brightness(1.05) hue-rotate(-10deg)',
                adjustments: { brightness: 105, hueRotate: -10 }
            },
            moon: {
                name: 'Moon',
                css: 'grayscale(1) contrast(1.1) brightness(1.1)',
                adjustments: { grayscale: 100, contrast: 110, brightness: 110 }
            },
            lark: {
                name: 'Lark',
                css: 'contrast(0.9) brightness(1.1) saturate(0.85)',
                adjustments: { contrast: 90, brightness: 110, saturate: 85 }
            },
            reyes: {
                name: 'Reyes',
                css: 'sepia(0.22) brightness(1.1) contrast(0.85) saturate(0.75)',
                adjustments: { sepia: 22, brightness: 110, contrast: 85, saturate: 75 }
            },
            juno: {
                name: 'Juno',
                css: 'sepia(0.35) contrast(1.15) brightness(1.15) saturate(1.8)',
                adjustments: { sepia: 35, contrast: 115, brightness: 115, saturate: 180 }
            },
            slumber: {
                name: 'Slumber',
                css: 'saturate(0.66) brightness(1.05) sepia(0.15)',
                adjustments: { saturate: 66, brightness: 105, sepia: 15 }
            },
            crema: {
                name: 'Crema',
                css: 'sepia(0.15) contrast(1.1) brightness(0.95) saturate(0.9)',
                adjustments: { sepia: 15, contrast: 110, brightness: 95, saturate: 90 }
            },
            ludwig: {
                name: 'Ludwig',
                css: 'saturate(0.8) contrast(1.05) brightness(1.05)',
                adjustments: { saturate: 80, contrast: 105, brightness: 105 }
            },
            aden: {
                name: 'Aden',
                css: 'hue-rotate(-20deg) contrast(0.9) saturate(0.85) brightness(1.2)',
                adjustments: { hueRotate: -20, contrast: 90, saturate: 85, brightness: 120 }
            },
            perpetua: {
                name: 'Perpetua',
                css: 'contrast(1.1) brightness(1.05) saturate(1.1)',
                adjustments: { contrast: 110, brightness: 105, saturate: 110 }
            },
            amaro: {
                name: 'Amaro',
                css: 'sepia(0.35) contrast(1.1) brightness(1.2) saturate(1.3)',
                adjustments: { sepia: 35, contrast: 110, brightness: 120, saturate: 130 }
            },
            rise: {
                name: 'Rise',
                css: 'brightness(1.05) sepia(0.2) contrast(0.9) saturate(0.9)',
                adjustments: { brightness: 105, sepia: 20, contrast: 90, saturate: 90 }
            },
            hudson: {
                name: 'Hudson',
                css: 'brightness(1.2) contrast(0.9) saturate(1.1)',
                adjustments: { brightness: 120, contrast: 90, saturate: 110 }
            },
            valencia: {
                name: 'Valencia',
                css: 'contrast(1.08) brightness(1.08) sepia(0.15)',
                adjustments: { contrast: 108, brightness: 108, sepia: 15 }
            },
            xpro2: {
                name: 'X-Pro II',
                css: 'sepia(0.3) contrast(1.3) brightness(0.9) saturate(1.5)',
                adjustments: { sepia: 30, contrast: 130, brightness: 90, saturate: 150 }
            },
            willow: {
                name: 'Willow',
                css: 'grayscale(0.5) contrast(0.95) brightness(0.9)',
                adjustments: { grayscale: 50, contrast: 95, brightness: 90 }
            },
            lofi: {
                name: 'Lo-Fi',
                css: 'saturate(1.1) contrast(1.5) brightness(0.95)',
                adjustments: { saturate: 110, contrast: 150, brightness: 95 }
            },
            inkwell: {
                name: 'Inkwell',
                css: 'sepia(0.3) contrast(1.1) brightness(1.1) grayscale(1)',
                adjustments: { sepia: 30, contrast: 110, brightness: 110, grayscale: 100 }
            },
            nashville: {
                name: 'Nashville',
                css: 'sepia(0.2) contrast(1.2) brightness(1.05) saturate(1.2)',
                adjustments: { sepia: 20, contrast: 120, brightness: 105, saturate: 120 }
            },
            vintage: {
                name: 'Vintage',
                css: 'sepia(0.4) contrast(0.9) brightness(1.1)',
                adjustments: { sepia: 40, contrast: 90, brightness: 110 }
            },
            kelvin: {
                name: 'Kelvin',
                css: 'sepia(0.15) contrast(1.2) brightness(1.1) saturate(1.4) hue-rotate(-10deg)',
                adjustments: { sepia: 15, contrast: 120, brightness: 110, saturate: 140, hueRotate: -10 }
            },
            mayfair: {
                name: 'Mayfair',
                css: 'contrast(1.1) saturate(1.1) brightness(1.05)',
                adjustments: { contrast: 110, saturate: 110, brightness: 105 }
            }
        };
    }

    /**
     * Obtiene la lista de filtros disponibles
     */
    getFilterList() {
        return Object.keys(this.filters).map(key => ({
            id: key,
            name: this.filters[key].name
        }));
    }

    /**
     * Aplica un filtro a un elemento (img o video)
     */
    applyFilter(element, filterId, intensity = 100) {
        if (!element || !this.filters[filterId]) return;

        this.currentFilter = filterId;
        this.filterIntensity = intensity;

        const filter = this.filters[filterId];

        if (filter.css === 'none') {
            element.style.filter = 'none';
        } else {
            // Aplicar con intensidad
            if (intensity === 100) {
                element.style.filter = filter.css;
            } else {
                element.style.filter = this.interpolateFilter(filterId, intensity);
            }
        }
    }

    /**
     * Interpola el filtro según la intensidad (0-100)
     */
    interpolateFilter(filterId, intensity) {
        const filter = this.filters[filterId];
        const adj = filter.adjustments;
        const factor = intensity / 100;

        let parts = [];

        if (adj.brightness !== undefined) {
            const val = 100 + (adj.brightness - 100) * factor;
            parts.push(`brightness(${val / 100})`);
        }
        if (adj.contrast !== undefined) {
            const val = 100 + (adj.contrast - 100) * factor;
            parts.push(`contrast(${val / 100})`);
        }
        if (adj.saturate !== undefined) {
            const val = 100 + (adj.saturate - 100) * factor;
            parts.push(`saturate(${val / 100})`);
        }
        if (adj.sepia !== undefined) {
            const val = adj.sepia * factor;
            parts.push(`sepia(${val / 100})`);
        }
        if (adj.grayscale !== undefined) {
            const val = adj.grayscale * factor;
            parts.push(`grayscale(${val / 100})`);
        }
        if (adj.hueRotate !== undefined) {
            const val = adj.hueRotate * factor;
            parts.push(`hue-rotate(${val}deg)`);
        }

        return parts.length > 0 ? parts.join(' ') : 'none';
    }

    /**
     * Obtiene el CSS del filtro actual
     */
    getCurrentFilterCSS() {
        if (this.filterIntensity === 100) {
            return this.filters[this.currentFilter].css;
        }
        return this.interpolateFilter(this.currentFilter, this.filterIntensity);
    }

    /**
     * Genera thumbnail con filtro aplicado
     */
    async generateFilteredThumbnail(source, filterId, width = 80, height = 80) {
        return new Promise((resolve) => {
            const canvas = document.createElement('canvas');
            canvas.width = width;
            canvas.height = height;
            const ctx = canvas.getContext('2d');

            const filter = this.filters[filterId];
            ctx.filter = filter.css === 'none' ? 'none' : filter.css;

            if (source instanceof HTMLVideoElement) {
                ctx.drawImage(source, 0, 0, width, height);
            } else if (source instanceof HTMLImageElement) {
                ctx.drawImage(source, 0, 0, width, height);
            } else if (source instanceof Blob) {
                const img = new Image();
                img.onload = () => {
                    ctx.drawImage(img, 0, 0, width, height);
                    URL.revokeObjectURL(img.src);
                    resolve(canvas.toDataURL('image/jpeg', 0.7));
                };
                img.src = URL.createObjectURL(source);
                return;
            }

            resolve(canvas.toDataURL('image/jpeg', 0.7));
        });
    }

    /**
     * Aplica filtro a un canvas para exportación
     */
    applyFilterToCanvas(ctx, width, height) {
        const filterCSS = this.getCurrentFilterCSS();
        if (filterCSS !== 'none') {
            ctx.filter = filterCSS;
        }
    }

    /**
     * Reset al filtro normal
     */
    reset() {
        this.currentFilter = 'normal';
        this.filterIntensity = 100;
    }
}
