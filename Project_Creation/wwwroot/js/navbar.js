/**
 * GrowTrack Navigation System
 * Enhanced navigation with modern functionality and better user experience
 */

document.addEventListener('DOMContentLoaded', function() {
    // Initialize variables
    const body = document.body;
    const sideNav = document.querySelector('.side-nav');
    const mainContent = document.querySelector('#wrapper');
    const toggleBtn = document.querySelector('#sideNavToggler');
    const mobileToggler = document.querySelector('#mobileNavToggler');
    const navLinks = document.querySelectorAll('.side-nav-link');
    const navDropdowns = document.querySelectorAll('.side-nav-dropdown');
    const overlay = document.querySelector('.mobile-nav-overlay');
    const topNav = document.querySelector('.top-nav');

    // Only initialize if we're on an authenticated page
    if (!body.classList.contains('user-authenticated')) return;

    // Initialize navigation state
    function initNavigation() {
        // Get the current collapse state
        const isCollapsed = localStorage.getItem('sidenavCollapsed') === 'true';
        
        // On mobile, always use expanded view
        if (window.innerWidth <= 991.98) {
            body.classList.remove('collapsed-nav');
            localStorage.setItem('sidenavCollapsed', 'false');
            
            if (toggleBtn) {
                const icon = toggleBtn.querySelector('i');
                const text = toggleBtn.querySelector('.control-text');
                if (icon) icon.className = 'fas fa-chevron-left';
                if (text) text.textContent = 'Collapse Menu';
            }
        } else {
            // For desktop, respect saved state
            if (isCollapsed) {
                body.classList.add('collapsed-nav');
                if (toggleBtn) {
                    const icon = toggleBtn.querySelector('i');
                    const text = toggleBtn.querySelector('.control-text');
                    if (icon) icon.className = 'fas fa-chevron-right';
                    if (text) text.textContent = 'Expand Menu';
                }
            }
        }

        // Set active states based on current URL
        const currentPath = window.location.pathname.toLowerCase();
        
        // Remove any existing active states first
        navLinks.forEach(link => link.classList.remove('active'));
        navDropdowns.forEach(dropdown => {
            dropdown.classList.remove('active');
            const parentItem = dropdown.closest('.side-nav-item');
            if (parentItem) parentItem.classList.remove('show');
        });

        // First, try to find exact matches
        let exactMatch = false;
        let activeSubmenu = false;

        navLinks.forEach(link => {
            const href = link.getAttribute('href')?.toLowerCase();
            if (href && currentPath === href) {
                exactMatch = true;
                link.classList.add('active');
                // Find parent dropdown if exists
                const parentDropdown = link.closest('.side-nav-item');
                if (parentDropdown) {
                    activeSubmenu = true;
                    const dropdownToggle = parentDropdown.querySelector('.side-nav-dropdown');
                    if (dropdownToggle) {
                        dropdownToggle.classList.add('active');
                        parentDropdown.classList.add('show');
                    }
                }
            }
        });

        // If no exact match found, try partial matches
        if (!exactMatch) {
            // First check for controller matches (higher priority)
            const urlParts = currentPath.split('/');
            const currentController = urlParts.length > 1 ? urlParts[1] : '';
            
            navLinks.forEach(link => {
                const href = link.getAttribute('href')?.toLowerCase();
                if (href) {
                    const hrefParts = href.split('/');
                    const hrefController = hrefParts.length > 1 ? hrefParts[1] : '';
                    
                    // Match by controller name
                    if (hrefController && currentController && hrefController === currentController) {
                        link.classList.add('active');
                        // Find parent dropdown if exists
                        const parentDropdown = link.closest('.side-nav-item');
                        if (parentDropdown) {
                            activeSubmenu = true;
                            const dropdownToggle = parentDropdown.querySelector('.side-nav-dropdown');
                            if (dropdownToggle) {
                                dropdownToggle.classList.add('active');
                                parentDropdown.classList.add('show');
                            }
                        }
                    }
                }
            });
            
            // If still no match, try path prefix matching
            if (!document.querySelector('.side-nav-link.active')) {
                navLinks.forEach(link => {
                    const href = link.getAttribute('href')?.toLowerCase();
                    if (href && href !== '/' && currentPath.startsWith(href)) {
                        link.classList.add('active');
                        // Find parent dropdown if exists
                        const parentDropdown = link.closest('.side-nav-item');
                        if (parentDropdown) {
                            activeSubmenu = true;
                            const dropdownToggle = parentDropdown.querySelector('.side-nav-dropdown');
                            if (dropdownToggle) {
                                dropdownToggle.classList.add('active');
                                parentDropdown.classList.add('show');
                            }
                        }
                    }
                });
            }
        }

        // If we have an active submenu, expand the navigation if it's collapsed
        if (activeSubmenu && isCollapsed && !body.classList.contains('nav-hover')) {
            // Don't auto-expand on page load, just add a visual indicator
            const activeDropdowns = document.querySelectorAll('.side-nav-dropdown.active');
            activeDropdowns.forEach(dropdown => {
                dropdown.classList.add('has-active-child');
            });
        }
    }

    // Toggle sidenav collapse state
    function toggleNav() {
        // Don't allow toggling on mobile devices
        if (window.innerWidth <= 991.98) {
            return;
        }
        
        body.classList.toggle('collapsed-nav');
        const isCollapsed = body.classList.contains('collapsed-nav');
        localStorage.setItem('sidenavCollapsed', isCollapsed);
        
        // Update toggle button
        const icon = toggleBtn.querySelector('i');
        const text = toggleBtn.querySelector('.control-text');
        const main = document.querySelector('#wrapper');
        if (icon) {
            icon.className = isCollapsed ? 'fas fa-chevron-right' : 'fas fa-chevron-left';
        }
        if (text) {
            text.textContent = isCollapsed ? 'Expand Menu' : 'Collapse Menu';
        }
        // Update top nav position
        if (topNav) {
            topNav.style.left = isCollapsed ? `${70}px` : `${280}px`;
        }
        if (main) {
            main.style.paddingLeft = isCollapsed ? `70px` : `280px`;
        }
    }

    // Handle mobile navigation
    function toggleMobileNav() {
        sideNav.classList.toggle('show');
        overlay.classList.toggle('show');
        body.style.overflow = sideNav.classList.contains('show') ? 'hidden' : '';
        document.querySelector('.nav-control-section').style.display = 'none !important';
    }

    // Close mobile nav when clicking outside
    function handleOverlayClick() {
        if (sideNav.classList.contains('show')) {
            toggleMobileNav();
        }
    }

    // Handle dropdown toggles
    function toggleDropdown(e) {
        e.preventDefault();
        const dropdownItem = this.closest('.side-nav-item');
        
        // Close all other dropdowns
        document.querySelectorAll('.side-nav-item.show').forEach(item => {
            if (item !== dropdownItem) {
                item.classList.remove('show');
                const toggle = item.querySelector('.side-nav-dropdown');
                if (toggle) toggle.classList.remove('active');
            }
        });

        // Toggle current dropdown
        dropdownItem.classList.toggle('show');
        this.classList.toggle('active');

        // If in collapsed mode and opening a dropdown, expand the nav
        if (body.classList.contains('collapsed-nav') && window.innerWidth > 991.98) {
            body.classList.remove('collapsed-nav');
            localStorage.setItem('sidenavCollapsed', 'false');
            
            // Update toggle button
            const icon = toggleBtn.querySelector('i');
            const text = toggleBtn.querySelector('.control-text');
            if (icon) {
                icon.className = 'fas fa-chevron-left';
            }
            if (text) {
                text.textContent = 'Collapse Menu';
            }
            
            // Update top nav position
            if (topNav) {
                topNav.style.left = `${280}px`;
            }
        }
    }

    // Handle window resize
    function handleResize() {
        if (window.innerWidth > 991.98) {
            // Switching to desktop view
            if (sideNav.classList.contains('show')) {
                sideNav.classList.remove('show');
                overlay.classList.remove('show');
                body.style.overflow = '';
            }
            
            // Apply saved collapse state
            const savedIsCollapsed = localStorage.getItem('sidenavCollapsed') === 'true';
            if (savedIsCollapsed && !body.classList.contains('collapsed-nav')) {
                body.classList.add('collapsed-nav');
                if (toggleBtn) {
                    const icon = toggleBtn.querySelector('i');
                    const text = toggleBtn.querySelector('.control-text');
                    if (icon) icon.className = 'fas fa-chevron-right';
                    if (text) text.textContent = 'Expand Menu';
                }
            }
            
            // Update top nav position based on current state
            if (topNav) {
                const isCollapsed = body.classList.contains('collapsed-nav');
                topNav.style.left = isCollapsed ? `${70}px` : `${280}px`;
            }
        } else {
            // Switching to mobile view - always use expanded nav
            body.classList.remove('collapsed-nav');
            
            // Show the toggle button text appropriately
            if (toggleBtn) {
                const icon = toggleBtn.querySelector('i');
                const text = toggleBtn.querySelector('.control-text');
                if (icon) icon.className = 'fas fa-chevron-left';
                if (text) text.textContent = 'Collapse Menu';
            }
            
            // Reset top nav position for mobile
            if (topNav) {
                topNav.style.left = '0';
            }
        }
    }

    // Initialize notifications
    function initNotifications() {
        const notificationList = document.querySelector('.notification-list');
        if (!notificationList) return;

        // Show loading state
        notificationList.innerHTML = '<div class="text-center py-3"><i class="fas fa-spinner fa-spin me-2"></i>Loading notifications...</div>';

        // Mock notifications for development
        const mockNotifications = [
            {
                type: 'info',
                message: 'Welcome to GrowTrack!',
                timeAgo: 'Just now',
                read: false
            },
            {
                type: 'success',
                message: 'Your inventory was updated successfully',
                timeAgo: '5 minutes ago',
                read: false
            },
            {
                type: 'warning',
                message: 'Low stock alert: Product XYZ',
                timeAgo: '1 hour ago',
                read: true
            }
        ];

        // Update with mock data
        setTimeout(() => {
            updateNotifications(mockNotifications);
        }, 1000);
    }

    // Initialize messages
    function initMessages() {
        const messageList = document.querySelector('.message-list');
        if (!messageList) return;

        // Show loading state
        messageList.innerHTML = '<div class="text-center py-3"><i class="fas fa-spinner fa-spin me-2"></i>Loading messages...</div>';

        // Mock messages for development
        const mockMessages = [
            {
                id: 1,
                sender: 'John Doe',
                preview: 'Hi, I would like to inquire about...',
                timeAgo: 'Just now',
                read: false
            },
            {
                id: 2,
                sender: 'Jane Smith',
                preview: 'Thank you for your quick response...',
                timeAgo: '30 minutes ago',
                read: true
            }
        ];

        // Update with mock data
        setTimeout(() => {
            updateMessages(mockMessages);
        }, 1000);
    }

    // Update notifications UI
    function updateNotifications(notifications) {
        const notificationList = document.querySelector('.notification-list');
        const badge = document.querySelector('.notification-badge');
        
        if (!notifications || notifications.length === 0) {
            notificationList.innerHTML = '<div class="text-center py-3">No new notifications</div>';
            badge.style.display = 'none';
            return;
        }

        let html = '';
        notifications.forEach(notification => {
            html += `
                <a href="#" class="dropdown-item notification-item ${notification.read ? 'read' : 'unread'}">
                    <div class="d-flex align-items-center">
                        <div class="notification-icon ${notification.type}">
                            <i class="fas ${getNotificationIcon(notification.type)}"></i>
                        </div>
                        <div class="notification-content">
                            <p class="mb-1">${notification.message}</p>
                            <small class="text-muted">${notification.timeAgo}</small>
                        </div>
                    </div>
                </a>`;
        });

        notificationList.innerHTML = html;
        const unreadCount = notifications.filter(n => !n.read).length;
        badge.textContent = unreadCount;
        badge.style.display = unreadCount > 0 ? 'flex' : 'none';
    }

    // Update messages UI
    function updateMessages(messages) {
        const messageList = document.querySelector('.message-list');
        const badge = document.querySelector('.message-badge');
        
        if (!messages || messages.length === 0) {
            messageList.innerHTML = '<div class="text-center py-3">No new messages</div>';
            badge.style.display = 'none';
            return;
        }

        let html = '';
        messages.forEach(message => {
            html += `
                <a href="#" class="dropdown-item message-item ${message.read ? 'read' : 'unread'}">
                    <div class="d-flex align-items-center">
                        <div class="message-avatar">
                            <i class="fas fa-user"></i>
                        </div>
                        <div class="message-content">
                            <h6 class="mb-1">${message.sender}</h6>
                            <p class="mb-1 text-truncate">${message.preview}</p>
                            <small class="text-muted">${message.timeAgo}</small>
                        </div>
                    </div>
                </a>`;
        });

        messageList.innerHTML = html;
        const unreadCount = messages.filter(m => !m.read).length;
        badge.textContent = unreadCount;
        badge.style.display = unreadCount > 0 ? 'flex' : 'none';
    }

    // Get notification icon based on type
    function getNotificationIcon(type) {
        const icons = {
            'alert': 'fa-exclamation-circle',
            'info': 'fa-info-circle',
            'success': 'fa-check-circle',
            'warning': 'fa-exclamation-triangle',
            'message': 'fa-envelope',
            'order': 'fa-shopping-cart'
        };
        return icons[type] || 'fa-bell';
    }

    // Event Listeners
    if (toggleBtn) {
        toggleBtn.addEventListener('click', toggleNav);
    }

    if (mobileToggler) {
        mobileToggler.addEventListener('click', toggleMobileNav);
    }

    if (overlay) {
        overlay.addEventListener('click', handleOverlayClick);
    }

    // Add click handlers for dropdown toggles
    navDropdowns.forEach(dropdown => {
        dropdown.addEventListener('click', toggleDropdown);
    });

    // Handle escape key press
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape' && sideNav.classList.contains('show')) {
            toggleMobileNav();
        }
    });

    // Handle window resize
    window.addEventListener('resize', handleResize);

    // Initialize
    initNavigation();
    initNotifications();
    initMessages();

    // Add hover functionality for collapsed state
    if (sideNav) {
        sideNav.addEventListener('mouseenter', function() {
            if (body.classList.contains('collapsed-nav') && window.innerWidth > 991.98) {
                body.classList.add('nav-hover');
            }
        });

        sideNav.addEventListener('mouseleave', function() {
            body.classList.remove('nav-hover');
        });
    }

    // Add hover functionality for the toggle button
    if (toggleBtn) {
        toggleBtn.addEventListener('mouseenter', function() {
            if (body.classList.contains('collapsed-nav') && window.innerWidth > 991.98) {
                const text = toggleBtn.querySelector('.control-text');
                if (text) {
                    text.style.opacity = '1';
                    text.style.visibility = 'visible';
                }
            }
        });
        
        toggleBtn.addEventListener('mouseleave', function() {
            if (body.classList.contains('collapsed-nav') && window.innerWidth > 991.98) {
                const text = toggleBtn.querySelector('.control-text');
                if (text && !body.classList.contains('nav-hover')) {
                    text.style.opacity = '';
                    text.style.visibility = '';
                }
            }
        });
    }

    // Mark all notifications as read
    document.querySelectorAll('.mark-all-read-btn').forEach(btn => {
        btn.addEventListener('click', function(e) {
            e.preventDefault();
            // Mark notifications as read logic
            const items = this.closest('.dropdown-menu').querySelectorAll('.notification-item, .message-item');
            items.forEach(item => {
                item.classList.remove('unread');
                item.classList.add('read');
            });

            // Update badge
            const badge = this.closest('.dropdown').querySelector('.notification-badge, .message-badge');
            if (badge) {
                badge.style.display = 'none';
                badge.textContent = '0';
            }
        });
    });

    // Set initial top nav position based on sidebar state
    if (topNav) {
        const isCollapsed = body.classList.contains('collapsed-nav');
        topNav.style.left = isCollapsed ? `${70}px` : `${280}px`;
    }
});
