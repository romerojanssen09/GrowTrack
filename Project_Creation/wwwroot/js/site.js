// Sidebar Toggle Functionality
document.addEventListener('DOMContentLoaded', function () {
    // Set initial state based on localStorage or default to collapsed
    const sidebarState = localStorage.getItem('sidebarState') || 'collapsed';
    
    if (sidebarState === 'expanded') {
        const wrapper = document.getElementById('wrapper');
        wrapper.classList.remove('toggled');
        // When expanded, show left arrow to indicate it can be collapsed
        const menuToggle = document.getElementById('menu-toggle');
        if (menuToggle) {
            menuToggle.innerHTML = '<i class="fas fa-angle-double-left"></i>';
        }
    } else {
        const wrapper = document.getElementById('wrapper');
        wrapper.classList.add('toggled');
        const menuToggle = document.getElementById('menu-toggle');
        if (menuToggle) {
            menuToggle.innerHTML = '<i class="fas fa-bars"></i>';
        }
    }

    // Toggle sidebar on button click
    const menuToggle = document.getElementById('menu-toggle');
    if (menuToggle) {
        menuToggle.addEventListener('click', function (e) {
            e.preventDefault();
            const wrapper = document.getElementById('wrapper');
            const isExpanded = wrapper.classList.contains('toggled');
            wrapper.classList.toggle('toggled');
            
            if (!isExpanded) {
                // When sidebar is expanded, show left arrow to indicate it can be collapsed
                menuToggle.innerHTML = '<i class="fas fa-angle-double-left"></i>';
                localStorage.setItem('sidebarState', 'expanded');
            } else {
                // When sidebar is collapsed, show hamburger menu icon
                menuToggle.innerHTML = '<i class="fas fa-bars"></i>';
                localStorage.setItem('sidebarState', 'collapsed');
            }
        });
    }

    // Highlight active menu item based on current URL
    highlightActiveMenuItem();

    // Set active class on sidebar items when clicked
    const sidebarItems = document.querySelectorAll('.sidebar-item');
    sidebarItems.forEach(item => {
        item.addEventListener('click', function () {
            // Remove active class from all items
            sidebarItems.forEach(i => i.classList.remove('active'));
            // Add active class to clicked item
            this.classList.add('active');
        });
    });

    // Handle window resize events
    window.addEventListener('resize', function () {
        adjustLayoutForScreenSize();
    });

    // Initial layout adjustment
    adjustLayoutForScreenSize();
});

let onetime = false;
// Function to update the toggle icon based on sidebar state
function updateToggleIcon(isExpanded) {
    const menuToggle = document.getElementById('menu-toggle');
    if (menuToggle) {
        if (isExpanded) {
            // When sidebar is collapsed, show hamburger menu icon
            menuToggle.innerHTML = '<i class="fas fa-bars"></i>';
            localStorage.setItem('sidebarState', 'collapsed');
        } else {
            // When sidebar is expanded, show left arrow to indicate it can be collapsed
            menuToggle.innerHTML = '<i class="fas fa-angle-double-left"></i>';
            localStorage.setItem('sidebarState', 'expanded');
        }
    }
}

// Function to adjust layout based on screen size
function adjustLayoutForScreenSize() {
    const wrapper = document.getElementById('wrapper');
    const windowWidth = window.innerWidth;

    if (windowWidth < 768) {
        // On mobile, sidebar should be hidden by default
        wrapper.classList.add('toggled');

        // No hover behavior - sidebar only expands on burger menu click
    } else {
        // On desktop, sidebar should be visible by default
        wrapper.classList.remove('toggled');
    }
}

// Function to highlight the active menu item based on current URL
function highlightActiveMenuItem() {
    const currentPath = window.location.pathname;
    const sidebarItems = document.querySelectorAll('.sidebar-item');

    sidebarItems.forEach(item => {
        const href = item.getAttribute('href');
        if (href && currentPath.includes(href)) {
            item.classList.add('active');
        }
    });
}

// Profile Dropdown Functionality
function toggleDropdown(event) {
    event.preventDefault();
    event.stopPropagation(); // Prevent immediate closing

    const menu = document.getElementById('profileDropdownMenu');
    const isOpen = menu.style.display === 'block';

    menu.style.display = isOpen ? 'none' : 'block';

    if (!isOpen) {
        // Only add listener when opening
        document.body.addEventListener('click', closeDropdownOnClickOutside, { once: true });
    }
}

function closeDropdownOnClickOutside(e) {
    const menu = document.getElementById('profileDropdownMenu');
    const trigger = document.getElementById('profileDropdown');

    // If click was outside the dropdown and trigger
    if (!menu.contains(e.target) && !trigger.contains(e.target)) {
        menu.style.display = 'none';
    }
}

// for signalR


// end of signalR

//let lastActivity = new Date();
//let currentStatus = 'online';
//let idleTimeLimit = 60 * 1000; // 1 minute

//function setStatus(newStatus) {
//    if (newStatus !== currentStatus) {
//        currentStatus = newStatus;
//        console.log("Status changed to:", newStatus);
//    }
//    // Do not send here – we send in the heartbeat below
//}

//function updateActivity() {
//    lastActivity = new Date();
//    setStatus('online');
//}

//window.addEventListener('mousemove', updateActivity);
//window.addEventListener('keydown', updateActivity);

//// Check for idle/away every 30 seconds
//setInterval(() => {
//    let now = new Date();
//    let idleTime = now - lastActivity;

//    if (idleTime >= idleTimeLimit && currentStatus !== 'away') {
//        setStatus('away');
//    }
//}, 30000);

//// Heartbeat to server every 60 seconds
//setInterval(() => {
//    fetch('/Online/Status', {
//        method: 'POST',
//        body: new URLSearchParams({ status: currentStatus }),
//        headers: {
//            'Content-Type': 'application/x-www-form-urlencoded',
//            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
//        }
//    });
//}, 60000);
