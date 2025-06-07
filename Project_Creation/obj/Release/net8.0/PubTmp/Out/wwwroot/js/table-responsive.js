/**
 * GrowTrack Table Responsiveness Handler
 * Ensures all tables are mobile-responsive
 */

(function() {
    // Make all tables responsive
    function makeTablesResponsive() {
        // Find all tables that aren't already in a .table-responsive container
        document.querySelectorAll('table.table').forEach(table => {
            // Skip if this table is already wrapped in a .table-responsive
            if (table.parentElement.classList.contains('table-responsive')) {
                return;
            }
            
            // Skip if this table is inside a DataTables wrapper (they have their own scrolling)
            if (table.closest('.dataTables_wrapper')) {
                // Add responsive class to DataTables wrapper if needed
                const wrapper = table.closest('.dataTables_wrapper');
                if (!wrapper.classList.contains('dt-responsive')) {
                    wrapper.classList.add('dt-responsive');
                }
                return;
            }
            
            // Create responsive wrapper
            const wrapper = document.createElement('div');
            wrapper.className = 'table-responsive position-relative';
            
            // Replace table with wrapper containing table
            table.parentNode.insertBefore(wrapper, table);
            wrapper.appendChild(table);
        });
    }

    // Function to enhance DataTables for mobile
    function enhanceDataTables() {
        // Find all DataTables wrappers
        document.querySelectorAll('.dataTables_wrapper').forEach(wrapper => {
            if (!wrapper.classList.contains('dt-responsive')) {
                wrapper.classList.add('dt-responsive', 'w-100');
            }
            
            // Make sure the table inside is also responsive
            const table = wrapper.querySelector('table');
            if (table && !table.classList.contains('responsive')) {
                table.classList.add('responsive');
            }
        });
    }

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        makeTablesResponsive();
        enhanceDataTables();
        
        // Set up mutation observer to watch for dynamic content
        const observer = new MutationObserver(function(mutations) {
            let shouldCheck = false;
            
            mutations.forEach(function(mutation) {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === 1) { // Element node
                        // If a table was added directly
                        if (node.tagName === 'TABLE') {
                            shouldCheck = true;
                        }
                        
                        // If a container with tables was added
                        if (node.querySelectorAll && node.querySelectorAll('table').length > 0) {
                            shouldCheck = true;
                        }
                        
                        // If a DataTables wrapper was added
                        if (node.classList && node.classList.contains('dataTables_wrapper')) {
                            shouldCheck = true;
                        }
                    }
                });
            });
            
            // Only run the expensive DOM operations if we need to
            if (shouldCheck) {
                makeTablesResponsive();
                enhanceDataTables();
            }
        });
        
        // Observe the entire body for changes
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    });
    
    // Also listen for window resize events to ensure tables stay responsive
    window.addEventListener('resize', function() {
        // Only run on mobile or when transitioning to mobile
        if (window.innerWidth <= 991.98) {
            enhanceDataTables();
        }
    });
    
    // Check responsiveness after page load (for dynamically loaded content)
    window.addEventListener('load', function() {
        setTimeout(function() {
            makeTablesResponsive();
            enhanceDataTables();
        }, 1000); // Small delay to ensure everything is loaded
    });
})(); 