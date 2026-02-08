document.addEventListener('DOMContentLoaded', () => {
    const csvFileInput = document.getElementById('csvFileInput');
    const chartConfigForm = document.getElementById('chartConfigForm');
    const barChartCanvas = document.getElementById('barChart');
    const categoryFieldSelect = document.getElementById('categoryField');
    const measureFieldSelect = document.getElementById('measureField');
    let chart;
    let currentDatasetId = null; // Переменная для хранения ID текущего датасета

    // --- Функция для заполнения выпадающих списков ---
    const populateSelect = (selectElement, options) => {
        selectElement.innerHTML = ''; // Очищаем старые опции
        options.forEach(optionValue => {
            const option = document.createElement('option');
            option.value = optionValue;
            option.textContent = optionValue;
            selectElement.appendChild(option);
        });
        selectElement.disabled = false;
    };

    // --- Загрузка CSV и заполнение списков ---
    csvFileInput.addEventListener('change', async (event) => {
        const file = event.target.files[0];
        if (!file) return;

        const formData = new FormData();
        formData.append('file', file);

        try {
            const response = await fetch('/api/V0/dataset', { method: 'POST', body: formData });
            if (response.ok) {
                const result = await response.json();
                alert(`Dataset uploaded successfully. ${result.count} records loaded.`);
                
                currentDatasetId = result.datasetId; // Сохраняем ID датасета

                // Заполняем списки
                populateSelect(categoryFieldSelect, result.categoryFields);
                populateSelect(measureFieldSelect, result.measureFields);

            } else {
                alert(`Error uploading dataset: ${await response.text()}`);
                categoryFieldSelect.disabled = true;
                measureFieldSelect.disabled = true;
                currentDatasetId = null;
            }
        } catch (error) {
            console.error('Error uploading dataset:', error);
            alert('An error occurred while uploading the dataset.');
            currentDatasetId = null;
        }
    });

    // --- Управление видимостью контролов ---
    const toggleControls = (checkboxId, controlsId) => {
        const checkbox = document.getElementById(checkboxId);
        const controls = document.getElementById(controlsId);
        checkbox.addEventListener('change', () => {
            controls.classList.toggle('hidden', !checkbox.checked);
        });
    };
    toggleControls('enableTop', 'topControls');
    toggleControls('enableSort', 'sortControls');
    toggleControls('enableColorCondition', 'colorConditionControls');
    toggleControls('enableReferenceLine', 'referenceLineControls');


    // --- Отправка формы и генерация графика ---
    chartConfigForm.addEventListener('submit', async (event) => {
        event.preventDefault();

        if (!currentDatasetId) {
            alert('Please upload a dataset first.');
            return;
        }

        const form = new FormData(chartConfigForm);
        const requestBody = {
            category_field: form.get('category_field'),
            measure: {
                field: form.get('measure_field'),
                aggregation: form.get('aggregation'),
            },
        };

        // Фильтр категорий
        const categoryFilter = form.get('category_filter').trim();
        if (categoryFilter) {
            requestBody.category_filter = categoryFilter.split(',').map(s => s.trim());
        }

        // Топ N
        if (form.get('enable_top')) {
            requestBody.top = {
                count: parseInt(form.get('top_count'), 10),
                sort: form.get('top_sort'),
            };
        }

        // Сортировка
        if (form.get('enable_sort')) {
            requestBody.sort = {
                field: form.get('sort_field'),
                sort: form.get('sort_order'),
            };
        }

        // Условное форматирование
        if (form.get('enable_color_condition')) {
            requestBody.color_condition = {
                color: form.get('color_condition_color'),
                comparison: form.get('color_condition_comparison'),
                value: parseInt(form.get('color_condition_value'), 10),
            };
        }

        // Опорная линия
        if (form.get('enable_reference_line')) {
            requestBody.reference_line = parseInt(form.get('reference_line_value'), 10);
        }

        try {
            const url = `/api/V0/viz/bar?dataset_id=${currentDatasetId}`;
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestBody),
            });

            if (response.ok) {
                const chartData = await response.json();
                renderChart(chartData);
            } else {
                alert(`Error generating chart: ${await response.text()}`);
            }
        } catch (error) {
            console.error('Error generating chart:', error);
            alert('An error occurred while generating the chart.');
        }
    });

    // --- Рендеринг графика ---
    function renderChart(data) {
        const chartData = {
            labels: data.data.map(item => item.category),
            datasets: [{
                label: 'Measure',
                data: data.data.map(item => item.measure),
                backgroundColor: data.data.map(item => {
                    if (!item.color) return 'rgba(75, 192, 192, 0.6)';
                    return item.color.toLowerCase();
                }),
                borderColor: data.data.map(item => {
                     if (!item.color) return 'rgba(75, 192, 192, 1)';
                    return item.color.toLowerCase();
                }),
                borderWidth: 1
            }]
        };

        const annotation = {
            type: 'line',
            scaleID: 'y',
            value: data.reference_line,
            borderColor: 'red',
            borderWidth: 2,
            label: {
                enabled: true,
                content: `Ref: ${data.reference_line}`,
                position: 'end'
            }
        };

        const options = {
            scales: { y: { beginAtZero: true } },
            plugins: {
                annotation: {
                    annotations: {}
                }
            }
        };
        
        if(data.reference_line !== null) {
            options.plugins.annotation.annotations.line1 = annotation;
        }


        if (chart) {
            chart.destroy();
        }

        chart = new Chart(barChartCanvas, {
            type: 'bar',
            data: chartData,
            options: options
        });
    }
});