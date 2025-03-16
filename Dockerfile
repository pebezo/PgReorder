FROM postgres:17.4

# Create a custom directory for tablespaces
RUN mkdir -p /tablespaces/tables
RUN mkdir -p /tablespaces/indexes

# Set permissions on the custom directory
RUN chown -R postgres:postgres /tablespaces/tables /tablespaces/indexes