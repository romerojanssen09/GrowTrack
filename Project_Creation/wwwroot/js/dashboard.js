// Dashboard Initialization and Management
document.addEventListener('DOMContentLoaded', function() {
    // Initialize dashboard if user is authenticated
    const isAuthenticated = document.body.classList.contains('user-authenticated');
    if (!isAuthenticated) return;

    // Initialize dashboard
    initDashboard();

    // Set interval to refresh data every 5 minutes
    setInterval(initDashboard, 300000);

    // Main dashboard initialization
    function initDashboard() {
        loadDashboardData();
        loadCalendarEvents();
        initActivityFilters();
    }

    // Load all dashboard data
    function loadDashboardData() {
        loadSummaryMetrics();
        loadInventoryOverview();
        loadLowStockAlerts();
        loadLeadsSummary();
        loadSalesTrend();
        loadRecentActivity();
    }

    // Format helpers
    function formatCurrency(amount) {
        return '₱' + parseFloat(amount).toFixed(2).replace(/\d(?=(\d{3})+\.)/g, '$&,');
    }

    function formatNumber(num) {
        return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
    }

    // Load summary metrics
    function loadSummaryMetrics() {
        const metricsContainer = document.getElementById('summaryMetrics');
        if (!metricsContainer) return;

        fetch('/Dashboard/GetSummaryMetrics')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    updateMetric('totalSales', formatCurrency(data.totalSales));
                    updateMetric('totalOrders', formatNumber(data.totalOrders));
                    updateMetric('averageOrderValue', formatCurrency(data.averageOrderValue));
                    updateMetric('totalProducts', formatNumber(data.totalProducts));
                }
            })
            .catch(error => {
                console.error('Error loading summary metrics:', error);
            });
    }

    // Update individual metric
    function updateMetric(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = value;
        }
    }

    // Load inventory overview
    function loadInventoryOverview() {
        const inventoryChart = document.getElementById('inventoryOverviewChart');
        if (!inventoryChart) return;

        fetch('/Dashboard/GetInventoryOverview')
            .then(response => response.json())
            .then(data => {
                if (data.success && data.categories) {
                    // Implementation for inventory chart
                    // Add your chart initialization code here
                }
            })
            .catch(error => {
                console.error('Error loading inventory overview:', error);
            });
    }

    // Load low stock alerts
    function loadLowStockAlerts() {
        const alertList = document.getElementById('lowStockList');
        const badge = document.getElementById('lowStockBadge');
        if (!alertList) return;

        fetch('/Dashboard/GetLowStockAlerts')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    if (data.alerts && data.alerts.length > 0) {
                        let html = '';
                        data.alerts.forEach(alert => {
                            html += `
                                <a href="/Inventory1/Details/${alert.productId}" class="list-group-item list-group-item-action py-2 px-3">
                                    <div class="d-flex justify-content-between align-items-center">
                                        <div>
                                            <div class="fw-bold">${alert.productName}</div>
                                            <small class="text-danger">Only ${alert.currentStock} units left</small>
                                        </div>
                                        <span class="badge bg-warning">${alert.currentStock}/${alert.reorderPoint}</span>
                                    </div>
                                </a>`;
                        });
                        alertList.innerHTML = html;
                        if (badge) {
                            badge.textContent = data.alerts.length;
                        }
                    } else {
                        alertList.innerHTML = '<div class="text-center py-3 text-muted">No low stock alerts</div>';
                        if (badge) {
                            badge.textContent = '0';
                        }
                    }
                }
            })
            .catch(error => {
                console.error('Error loading low stock alerts:', error);
                alertList.innerHTML = '<div class="text-center py-3">Error loading alerts</div>';
            });
    }

    // Load leads summary
    function loadLeadsSummary() {
        const elements = {
            totalLeads: document.getElementById('totalLeads'),
            newLeads: document.getElementById('newLeads'),
            contactedLeads: document.getElementById('contactedLeads'),
            recentLeads: document.getElementById('recentLeads'),
            progressNew: document.getElementById('progressNew'),
            progressContacted: document.getElementById('progressContacted'),
            keyMetricTotalLeads: document.getElementById('keyMetricTotalLeads'),
            keyMetricNewLeads: document.getElementById('keyMetricNewLeads')
        };

        // Skip if the main leads overview element doesn't exist
        if (!elements.totalLeads) return;

        fetch('/Dashboard/GetLeadsSummary')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    updateLeadsData(data, elements);
                }
            })
            .catch(error => {
                console.error('Error loading leads summary:', error);
            });
    }

    // Update leads data
    function updateLeadsData(data, elements) {
        elements.totalLeads.textContent = formatNumber(data.totalLeads);
        elements.newLeads.textContent = formatNumber(data.newLeads);
        elements.contactedLeads.textContent = formatNumber(data.contactedLeads);
        elements.recentLeads.textContent = formatNumber(data.recentLeads);

        if (elements.keyMetricTotalLeads) {
            elements.keyMetricTotalLeads.textContent = formatNumber(data.totalLeads);
        }

        if (elements.keyMetricNewLeads) {
            elements.keyMetricNewLeads.textContent = formatNumber(data.newLeads);
        }

        // Update progress bars
        if (data.totalLeads > 0) {
            const newPercent = (data.newLeads / data.totalLeads) * 100;
            const contactedPercent = (data.contactedLeads / data.totalLeads) * 100;

            if (elements.progressNew) {
                elements.progressNew.style.width = newPercent + '%';
            }
            if (elements.progressContacted) {
                elements.progressContacted.style.width = contactedPercent + '%';
            }
        }
    }

    // Load sales trend
    function loadSalesTrend() {
        const chartElement = document.getElementById('salesTrendChart');
        if (!chartElement) return;

        fetch('/Dashboard/GetSalesTrendByDays/7')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    // Implementation for sales trend chart
                    // Add your chart initialization code here
                }
            })
            .catch(error => {
                console.error('Error loading sales trend:', error);
            });
    }

    // Load recent activity
    function loadRecentActivity() {
        const activityFeed = document.getElementById('recentActivity');
        if (!activityFeed) return;

        fetch('/Dashboard/GetRecentActivity?limit=10')
            .then(response => response.json())
            .then(data => {
                if (data.success && data.activities) {
                    updateActivityFeed(data.activities, activityFeed);
                }
            })
            .catch(error => {
                console.error('Error loading recent activity:', error);
                activityFeed.innerHTML = '<div class="text-center py-4 text-danger">Failed to load recent activity</div>';
            });
    }

    // Update activity feed
    function updateActivityFeed(activities, container) {
        if (activities.length === 0) {
            container.innerHTML = '<div class="text-center py-4 text-muted">No recent activity</div>';
            return;
        }

        let html = '';
        activities.forEach(activity => {
            const activityHtml = createActivityItem(activity);
            if (activityHtml) {
                html += activityHtml;
            }
        });
        container.innerHTML = html;
    }

    // Create activity item HTML
    function createActivityItem(activity) {
        let icon = '';
        let color = '';
        
        switch (activity.type.toLowerCase()) {
            case 'sale':
                icon = 'fa-shopping-cart';
                color = 'text-success';
                break;
            case 'stockin':
                icon = 'fa-box';
                color = 'text-primary';
                break;
            case 'stockout':
                icon = 'fa-box-open';
                color = 'text-warning';
                break;
            case 'lead':
                icon = 'fa-user-plus';
                color = 'text-info';
                break;
            default:
                icon = 'fa-info-circle';
                color = 'text-secondary';
        }

        return `
            <div class="activity-item ${activity.type.toLowerCase()}">
                <div class="activity-content">
                    <div class="activity-icon ${color}">
                        <i class="fas ${icon}"></i>
                    </div>
                    <div class="activity-details">
                        <div class="activity-text">${activity.description}</div>
                        <div class="activity-time">${activity.timeAgo}</div>
                    </div>
                </div>
            </div>`;
    }

    // Initialize activity filters
    function initActivityFilters() {
        const filterButtons = {
            showSales: document.getElementById('showSales'),
            showStock: document.getElementById('showStock'),
            showLeads: document.getElementById('showLeads'),
            showAll: document.getElementById('showAll')
        };

        // Add click handlers for filter buttons
        Object.entries(filterButtons).forEach(([key, button]) => {
            if (button) {
                button.addEventListener('click', (e) => {
                    e.preventDefault();
                    filterActivities(key);
                });
            }
        });
    }

    // Filter activities
    function filterActivities(filterType) {
        const items = document.querySelectorAll('.activity-item');
        if (!items.length) return;

        items.forEach(item => {
            switch (filterType) {
                case 'showSales':
                    item.style.display = item.classList.contains('sale') ? '' : 'none';
                    break;
                case 'showStock':
                    item.style.display = (item.classList.contains('stockin') || item.classList.contains('stockout')) ? '' : 'none';
                    break;
                case 'showLeads':
                    item.style.display = item.classList.contains('lead') ? '' : 'none';
                    break;
                case 'showAll':
                    item.style.display = '';
                    break;
            }
        });
    }

    // Load calendar events
    function loadCalendarEvents() {
        const eventsContainer = document.getElementById('upcomingEvents');
        if (!eventsContainer) return;

        fetch('/Calendar/GetUpcomingEvents')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    updateCalendarEvents(data.events, eventsContainer);
                }
            })
            .catch(error => {
                console.error('Error loading calendar events:', error);
                eventsContainer.innerHTML = '<div class="text-center py-3 text-danger">Failed to load events</div>';
            });
    }

    // Update calendar events
    function updateCalendarEvents(events, container) {
        if (!events || events.length === 0) {
            container.innerHTML = '<div class="text-center py-3 text-muted">No upcoming events</div>';
            return;
        }

        const today = new Date();
        let html = '';

        events.forEach(event => {
            const eventDate = new Date(event.date);
            if (isValidEventToShow(eventDate, today, event)) {
                html += createEventItem(event, eventDate, today);
            }
        });

        container.innerHTML = html || '<div class="text-center py-3 text-muted">No upcoming events</div>';
        updateTasksDateHeader();
    }

    // Check if event should be shown
    function isValidEventToShow(eventDate, today, event) {
        const eventTime = eventDate.getHours() * 60 + eventDate.getMinutes();
        const currentTime = today.getHours() * 60 + today.getMinutes();
        const isToday = eventDate.toDateString() === today.toDateString();

        return !(isToday && eventTime < currentTime && event.isCompleted);
    }

    // Create event item HTML
    function createEventItem(event, eventDate, today) {
        const dateDisplay = getDateDisplay(eventDate, today);
        const timeDisplay = getTimeDisplay(eventDate);
        const priorityInfo = getPriorityInfo(event.priority);

        return `
            <a href="/Calendar" class="list-group-item list-group-item-action ${priorityInfo.class} py-2 px-3">
                <div class="d-flex justify-content-between align-items-center">
                    <div class="text-truncate">
                        ${priorityInfo.badge}<span class="${event.isCompleted ? 'text-decoration-line-through text-muted' : ''}">${event.title}</span>
                    </div>
                    <div class="d-flex align-items-center">
                        ${timeDisplay}
                        <span class="ms-2">${dateDisplay}</span>
                    </div>
                </div>
            </a>`;
    }

    // Get date display format
    function getDateDisplay(eventDate, today) {
        const tomorrow = new Date(today);
        tomorrow.setDate(today.getDate() + 1);

        if (eventDate.toDateString() === today.toDateString()) {
            return '<span class="badge bg-primary">Today</span>';
        } else if (eventDate.toDateString() === tomorrow.toDateString()) {
            return '<span class="badge bg-info">Tomorrow</span>';
        } else {
            return eventDate.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        }
    }

    // Get time display format
    function getTimeDisplay(date) {
        return date.toLocaleTimeString('en-US', { 
            hour: 'numeric', 
            minute: '2-digit', 
            hour12: true 
        });
    }

    // Get priority information
    function getPriorityInfo(priority) {
        const priorityMap = {
            0: { class: '', badge: '' },
            1: { class: 'border-start border-danger', badge: '<span class="badge bg-danger me-2">High</span>' },
            2: { class: 'border-start border-warning', badge: '<span class="badge bg-warning me-2">Medium</span>' },
            3: { class: 'border-start border-info', badge: '<span class="badge bg-info me-2">Low</span>' }
        };

        return priorityMap[priority] || priorityMap[0];
    }

    // Update tasks date header
    function updateTasksDateHeader() {
        const dateHeader = document.getElementById('tasksDateHeader');
        if (!dateHeader) return;

        const today = new Date();
        const formattedDate = today.toLocaleDateString('en-US', {
            month: 'long',
            day: 'numeric',
            year: 'numeric'
        });

        dateHeader.textContent = `Today's Appointments: ${formattedDate}`;
    }
}); 